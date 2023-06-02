namespace Flow.Plugin.Github

open System
open System.IO
open System.Text.Json.Serialization
open System.Threading
open System.Threading.Tasks
open Flow.Launcher.Plugin
open System.Collections.Generic
open Humanizer
open IcedTasks

type Settings() =
    [<JsonInclude>]
    member val GithubApiToken : string = "" with get, set

type GithubPlugin() =

    let mutable pluginContext = PluginInitContext()

    let mutable pluginSettings = Settings()

    let getSettingsFilePath () =
        Path.Combine (pluginContext.CurrentPluginMetadata.PluginDirectory, "..", "..", "Settings", "Plugins", "Flow.Plugin.Github", "Settings.json")

    let openUrl (url:string) =
        do pluginContext.API.OpenUrl url
        true

    let createQuery request =
        let subQuery =
            match request with
            | FindRepos search              -> $"repos {search}"
            | FindUsers search              -> $"users {search}"
            | FindIssues (user, repo)       -> $"{user}/{repo}/issues"
            | FindPRs (user, repo)          -> $"{user}/{repo}/pulls"
            | FindIssueOrPr (user, repo, n) -> $"{user}/{repo}#{n}"
            | FindRepo (user, repo)         -> $"{user}/{repo}"
            | FindUserRepos user            -> $"{user}/"

        let keyword =
            if isNull pluginContext.CurrentPluginMetadata
            then "gh"
            else pluginContext.CurrentPluginMetadata.ActionKeyword

        $"{keyword} {subQuery}"

    let changeQuery request =
        do pluginContext.API.ChangeQuery (createQuery request)
        false

    /// ApiSearchResult -> SearchResult list
    let presentApiSearchResult = function
        | Repos [] | RepoIssues [] | RepoPRs [] | Users [] ->
            [   Result (
                    Title    = "No results found",
                    SubTitle = "please try a different query" ) ]
        | Repos repos ->
            [ for r in repos ->
                Result (
                    Title    = r.FullName,
                    SubTitle = sprintf "(★%d | %s) %s" r.StargazersCount r.Language r.Description,
                    CopyText = r.HtmlUrl,
                    AutoCompleteText = createQuery (FindRepo (r.Owner.Login, r.Name)),
                    Action   = fun ctx ->
                                if ctx.SpecialKeyState.CtrlPressed
                                then openUrl r.HtmlUrl
                                else changeQuery (FindRepo (r.Owner.Login, r.Name)) ) ]
        | RepoIssues issues ->
            [ for i in issues ->
                Result (
                    Title    = i.Title,
                    SubTitle = sprintf "issue #%d | %d comments | created %s by %s" i.Number i.Comments (i.CreatedAt.Humanize()) i.User.Login,
                    CopyText = i.HtmlUrl,
                    Action   = fun _ -> openUrl i.HtmlUrl ) ]
        | RepoIssueOrPr issue ->
            [   Result (
                    Title    = sprintf "#%d - %s" issue.Number issue.Title,
                    SubTitle = sprintf "%A | created by %s | last updated %s" issue.State issue.User.Login (issue.UpdatedAt.Humanize()),
                    CopyText = issue.HtmlUrl,
                    Action   = fun _ -> openUrl issue.HtmlUrl ) ]
        | RepoPRs issues ->
            [ for i in issues ->
                Result (
                    Title    = i.Title,
                    SubTitle = sprintf "PR #%d | %d comments | created %s by %s" i.Number i.Comments (i.CreatedAt.Humanize()) i.User.Login,
                    CopyText = i.HtmlUrl,
                    Action   = fun _ -> openUrl i.HtmlUrl ) ]
        | Users users ->
            [ for u in users ->
                Result (
                    Title    = u.Login,
                    SubTitle = u.HtmlUrl,
                    CopyText = u.HtmlUrl,
                    AutoCompleteText = createQuery (FindUserRepos u.Login),
                    Action   = fun _ -> openUrl u.HtmlUrl ) ]
        | RepoDetails (res, issues, prs) ->
            [   Result (
                    Title    = res.FullName,
                    SubTitle = sprintf "(★%d | %s) %s" res.StargazersCount res.Language res.Description,
                    CopyText = res.HtmlUrl,
                    Score    = 300,
                    Action   = fun _ -> openUrl res.HtmlUrl)
                Result (
                    Title    = "Issues",
                    SubTitle = sprintf "%d issues open" (List.length issues),
                    CopyText = res.HtmlUrl + "/issues",
                    AutoCompleteText = createQuery (FindIssues (res.Owner.Login, res.Name)),
                    Score    = 200,
                    Action   = fun ctx ->
                                if ctx.SpecialKeyState.CtrlPressed
                                then openUrl (res.HtmlUrl + "/issues")
                                else changeQuery (FindIssues (res.Owner.Login, res.Name)) )
                Result (
                    Title    = "Pull Requests",
                    SubTitle = sprintf "%d pull requests open" (List.length prs),
                    CopyText = res.HtmlUrl + "/pulls",
                    AutoCompleteText = createQuery (FindPRs (res.Owner.Login, res.Name)),
                    Score    = 100,
                    Action   = fun ctx ->
                                if ctx.SpecialKeyState.CtrlPressed
                                then openUrl (res.HtmlUrl + "/pulls")
                                else changeQuery (FindPRs (res.Owner.Login, res.Name)) ) ]

    /// QuerySuggestion -> SearchResult list
    let presentSuggestion = function
        | SearchRepos search ->
            [   Result (
                    Title    = "Search repositories",
                    SubTitle = sprintf "Search for repositories matching \"%s\"" search,
                    Action   = fun _ -> changeQuery (FindRepos search) )
                Result (
                    Title    = "Search users",
                    SubTitle = sprintf "Search for users matching \"%s\"" search,
                    Action   = fun _ -> changeQuery (FindUsers search) ) ]
        | DefaultSuggestion ->
            [   Result (
                    Title    = "Search repositories",
                    SubTitle = "Search Github repositories with \"gh repos {repo-search-term}\"",
                    Action   = fun _ -> changeQuery (FindRepos "") )
                Result (
                    Title    = "Search users",
                    SubTitle = "Search Github users with \"gh users {user-search-term}\"",
                    Action   = fun _ -> changeQuery (FindUsers "") ) ]

    let presentIncompleteQuery =
        [ Result (
            Title = "Search Github",
            SubTitle = "type a search term") ]

    /// exn -> SearchResult list
    let presentApiSearchExn (e: exn) =
        match e with
        | null ->
            [ Result (Title = "Search failed") ]
        | :? Octokit.RateLimitExceededException ->
            [ Result (Title = "Rate limit exceeded", SubTitle = "please try again later") ]
        | :? Octokit.NotFoundException ->
            [ Result (Title = "Search failed", SubTitle = "The resource could not be found") ]
        | _ ->
            [ Result (Title = "Search failed", SubTitle = e.Message) ]

    let tryRunApiSearch (fSearch: CancellableTask<_>) =
        cancellableTask {
            try
                let! result = fSearch
                return presentApiSearchResult result
            with exn ->
                return presentApiSearchExn exn
        }

    member this.CheckSavedPluginSettings =
        let mutable lastCheck = DateTime.Today

        fun () ->
            let settingsFilePath = getSettingsFilePath ()

            if File.Exists settingsFilePath then
                let actualLwt = File.GetLastWriteTimeUtc settingsFilePath

                if lastCheck <> actualLwt then
                    pluginSettings <- pluginContext.API.LoadSettingJsonStorage<Settings>()
                    lastCheck <- actualLwt
                    do GithubApi.setApiToken pluginSettings.GithubApiToken
            else
                // settings file should be created with defaults by calling LoadSettingJsonStorage
                pluginSettings <- pluginContext.API.LoadSettingJsonStorage<Settings>()
                lastCheck <- File.GetLastWriteTimeUtc settingsFilePath
                do GithubApi.setApiToken pluginSettings.GithubApiToken

    member this.ProcessQuery terms =
        match terms with
        | CompleteQuery apiSearch -> tryRunApiSearch (Cache.memoize Gh.runSearch apiSearch)
        | BadQuery suggestion     -> CancellableTask.singleton (presentSuggestion suggestion)
        | IncompleteQuery         -> CancellableTask.singleton presentIncompleteQuery

    interface IAsyncPlugin with
        member this.InitAsync(context: PluginInitContext) =

            pluginContext <- context

            // check if the settings file has been modified
            do this.CheckSavedPluginSettings()

            Task.CompletedTask

        member this.QueryAsync(query: Query, token: CancellationToken) =
            task {
                // check if the settings file has been modified
                do this.CheckSavedPluginSettings()

                let! results =
                    this.ProcessQuery (List.ofArray query.SearchTerms) token

                for result in results do
                    result.IcoPath <- "icon.png"

                return List<Result> results
            }

    interface IReloadable with
        member this.ReloadData () =
            Cache.clear ()
