module Flow.Plugin.Github.Cache

open System
open System.Collections.Concurrent
open IcedTasks

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
