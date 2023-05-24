namespace Flow.Plugin.Github

open Octokit
open System
open System.Collections.Concurrent
open IcedTasks

type ApiSearchRequest =
    | FindRepos of string
    | FindUsers of string
    | FindIssues of string * string
    | FindPRs of string * string
    | FindIssueOrPr of string * string * int
    | FindRepo of string * string
    | FindUserRepos of string

type ApiSearchResult =
    | Repos of Repository list
    | RepoIssues of Issue list
    | RepoIssueOrPr of Issue
    | RepoPRs of Issue list
    | Users of User list
    | RepoDetails of Repository * Issue list * Issue list

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

module Cache =

    let private resultCache =
        ConcurrentDictionary<ApiSearchRequest, ApiSearchResult * DateTime>()

    let private cacheAge = TimeSpan.FromHours 2.0

    /// convert inner key values to lower-case
    let private getActualKey = function
        | FindRepos s -> FindRepos (toLower s)
        | FindUsers s -> FindUsers (toLower s)
        | FindIssues (u,r) -> FindIssues (toLower u, toLower r)
        | FindPRs (u,r) -> FindPRs (toLower u, toLower r)
        | FindIssueOrPr (u,r,i) -> FindIssueOrPr (toLower u, toLower r, i)
        | FindRepo (u,r) -> FindRepo (toLower u, toLower r)
        | FindUserRepos s -> FindUserRepos (toLower s)

    let private addToCache (key, value) =
        let valueWithAge = (value, DateTime.Now.Add cacheAge)
        resultCache.TryAdd (getActualKey key, valueWithAge) |> ignore

    let memoize (fCompute: _ -> CancellableTask<_>) key = cancellableTask {
        match resultCache.TryGetValue (getActualKey key) with
        | true, (res,exp) when exp > DateTime.Now ->
            return res
        | _ ->
            let! result = fCompute key

            addToCache (key, result)

            match key, result with
            | FindRepo(u,r), RepoDetails(_,issues,prs) ->
                // cache repo PRs
                addToCache (FindPRs(u,r), RepoPRs prs)
                // cache repo issues
                addToCache (FindIssues(u,r), RepoIssues issues)
                // cache every issue by number
                for issue in issues @ prs do
                    addToCache (FindIssueOrPr (u,r,issue.Number), RepoIssueOrPr issue)
            | FindIssues(u,r), RepoIssues issues ->
                // cache every issue by number
                for issue in issues do
                    addToCache (FindIssueOrPr (u,r,issue.Number), RepoIssueOrPr issue)
            | FindPRs(u,r), RepoPRs pulls ->
                // cache every PR by number
                for pr in pulls do
                    addToCache (FindIssueOrPr (u,r,pr.Number), RepoIssueOrPr pr)
            | _ ->
                ()

            return result
    }

    let clear () = 
        resultCache.Clear()

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

    let runSearchCached search =
        Cache.memoize runSearch search
