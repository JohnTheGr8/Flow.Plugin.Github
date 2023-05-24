namespace Flow.Plugin.Github

open Octokit
open System
open System.Collections.Concurrent
open IcedTasks

/// Cache for all GET requests to the Github API, used by Octokit
/// to create conditional http requests.
type GithubApiResponseCache() =

    let cache = ConcurrentDictionary<string, Octokit.Caching.CachedResponse.V1> ()

    interface Octokit.Caching.IResponseCache with
        member _.GetAsync(request) =
            task {
                return cache.[request.Endpoint.OriginalString]
            }

        member _.SetAsync(request, response) =
            task {
                do cache.[request.Endpoint.OriginalString] <- response
            }

module GithubApi =

    let private client =
        let credentials =
            match tryLoadGithubToken () with
            | Some token -> Credentials token
            | None -> Credentials.Anonymous

        GitHubClient(
            ProductHeaderValue "Flow.Plugin.Github",
            Credentials = credentials,
            ResponseCache = GithubApiResponseCache()
        )

    let getRepositories (search: string) = cancellableTask {
        let! results = client.Search.SearchRepo (SearchRepositoriesRequest search)
        return List.ofSeq results.Items |> Repos
    }

    let getUsers (search: string) = cancellableTask {
        let! results = client.Search.SearchUsers(SearchUsersRequest search)
        return List.ofSeq results.Items |> Users
    }

    let getUserRepos (owner: string) = cancellableTask {
        let! results = client.Repository.GetAllForUser owner
        return List.ofSeq results |> List.sortByDescending (fun repo -> repo.UpdatedAt) |> Repos
    }

    let getIssuesAndPRs (user: string) (repo: string) = cancellableTask {
        let! results = client.Issue.GetAllForRepository(user, repo)
        return List.ofSeq results
    }

    let isNotPullRequest (i:Issue) = isNull i.PullRequest

    let getRepoIssues user repo = cancellableTask {
        let! data = getIssuesAndPRs user repo
        return data |> List.filter isNotPullRequest |> RepoIssues
    }

    let getRepoPRs (user: string) (repo: string) = cancellableTask {
        let! data = getIssuesAndPRs user repo
        return data |> List.filter (isNotPullRequest >> not) |> RepoPRs
    }

    let getRepoInfo (user: string) (repo: string) = cancellableTask {
        let! repository = client.Repository.Get(user,repo)
        and! issuesAndPRs = getIssuesAndPRs user repo
        let issues, prs = issuesAndPRs |> List.partition isNotPullRequest

        return RepoDetails (repository, issues, prs)
    }

    let getSpecificIssue (user: string) (repo: string) (issue: int) = cancellableTask {
        let! data = client.Issue.Get(user, repo, issue)
        return RepoIssueOrPr data
    }

module Gh =

    let runSearch = function
        | FindRepos search              -> GithubApi.getRepositories search
        | FindUsers search              -> GithubApi.getUsers search
        | FindIssues (user, repo)       -> GithubApi.getRepoIssues user repo
        | FindPRs (user, repo)          -> GithubApi.getRepoPRs user repo
        | FindIssueOrPr (user, repo, n) -> GithubApi.getSpecificIssue user repo n
        | FindRepo (user, repo)         -> GithubApi.getRepoInfo user repo
        | FindUserRepos user            -> GithubApi.getUserRepos user
