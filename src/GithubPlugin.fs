namespace Flow.Plugin.Github

open System.Threading
open System.Threading.Tasks
open Flow.Launcher.Plugin
open System.Collections.Generic
open Humanizer
open IcedTasks

type ActionForQuery =
    | RunApiSearch of CancellableTask<ApiSearchResult>
    | SuggestQuery of QuerySuggestion

and QuerySuggestion =
    | SearchRepos of string
    | DefaultSuggestion

type SearchResult = { title : string ; subtitle : string; action : ActionContext -> bool }

type GithubPlugin() =

    let runApiSearch = Gh.runSearchCached >> RunApiSearch

    let parseQuery = function
        | "repos" :: search                       -> runApiSearch (FindRepos (String.concat " " search))
        | "users" :: search                       -> runApiSearch (FindUsers (String.concat " " search))
        | [ "issues"; UserRepoFormat search ]     -> runApiSearch (FindIssues search)
        | [ "pr";     UserRepoFormat search ]     -> runApiSearch (FindPRs search)
        | [ "pull";   UserRepoFormat search ]     -> runApiSearch (FindPRs search)
        | [ "repo";   UserRepoFormat search ]     -> runApiSearch (FindRepo search)
        | [ UserRepoFormat search           ]     -> runApiSearch (FindRepo search)
        | [ UserRepoFormat search; "issues" ]     -> runApiSearch (FindIssues search)
        | [ UserRepoFormat search; "pr"     ]     -> runApiSearch (FindPRs search)
        | [ UserRepoFormat search; "pull"   ]     -> runApiSearch (FindPRs search)
        | [ UserRepoFormat (u,r); IssueFormat i ] -> runApiSearch (FindIssueOrPr (u,r,i))
        | [ UserReposFormat user ]                -> runApiSearch (FindUserRepos user)
        | [ search ]                              -> SuggestQuery (SearchRepos search)
        | _                                       -> SuggestQuery DefaultSuggestion

    let mutable pluginContext = PluginInitContext()

    let openUrl (url:string) =
        do pluginContext.API.OpenUrl url
        true

    let changeQuery (newQuery:string) (newParam:string) =
        pluginContext.API.ChangeQuery <| sprintf "%s %s %s" pluginContext.CurrentPluginMetadata.ActionKeyword newQuery newParam
        false

    /// ApiSearchResult -> SearchResult list
    let presentApiSearchResult = function
        | Repos [] | RepoIssues [] | RepoPRs [] | Users [] ->
            [   { title    = "No results found"
                  subtitle = "please try a different query"
                  action   = fun _ -> false } ]
        | Repos repos ->
            [ for r in repos ->
                { title    = r.FullName
                  subtitle = sprintf "(★%d | %s) %s" r.StargazersCount r.Language r.Description
                  action   = fun ctx ->
                                if ctx.SpecialKeyState.CtrlPressed
                                then openUrl r.HtmlUrl
                                else changeQuery "repo" r.FullName } ]
        | RepoIssues issues ->
            [ for i in issues ->
                { title    = i.Title
                  subtitle = sprintf "issue #%d | %d comments | created %s by %s" i.Number i.Comments (i.CreatedAt.Humanize()) i.User.Login
                  action   = fun _ -> openUrl i.HtmlUrl } ]
        | RepoIssueOrPr issue ->
            [   { title    = sprintf "#%d - %s" issue.Number issue.Title
                  subtitle = sprintf "%A | created by %s | last updated %s" issue.State issue.User.Login (issue.UpdatedAt.Humanize())
                  action   = fun _ -> openUrl issue.HtmlUrl } ]
        | RepoPRs issues ->
            [ for i in issues ->
                { title    = i.Title
                  subtitle = sprintf "PR #%d | %d comments | created %s by %s" i.Number i.Comments (i.CreatedAt.Humanize()) i.User.Login
                  action   = fun _ -> openUrl i.HtmlUrl } ]
        | Users users ->
            [ for u in users ->
                { title    = u.Login
                  subtitle = u.HtmlUrl
                  action   = fun _ -> openUrl u.HtmlUrl } ]
        | RepoDetails (res, issues, prs) ->
            [   { title    = res.FullName
                  subtitle = sprintf "(★%d | %s) %s" res.StargazersCount res.Language res.Description
                  action   = fun _ -> openUrl res.HtmlUrl };
                { title    = "Issues"
                  subtitle = sprintf "%d issues open" (List.length issues)
                  action   = fun ctx ->
                                if ctx.SpecialKeyState.CtrlPressed
                                then openUrl (res.HtmlUrl + "/issues")
                                else changeQuery "issues" res.FullName };
                { title    = "Pull Requests"
                  subtitle = sprintf "%d pull requests open" (List.length prs)
                  action   = fun ctx ->
                                if ctx.SpecialKeyState.CtrlPressed
                                then openUrl (res.HtmlUrl + "/pulls")
                                else changeQuery "pr" res.FullName } ]

    /// QuerySuggestion -> SearchResult list
    let presentSuggestion = function
        | SearchRepos search ->
            [   { title    = "Search repositories"
                  subtitle = sprintf "Search for repositories matching \"%s\"" search
                  action   = fun _ -> changeQuery "repos" search };
                { title    = "Search users"
                  subtitle = sprintf "Search for users matching \"%s\"" search
                  action   = fun _ -> changeQuery "users" search } ]
        | DefaultSuggestion ->
            [   { title    = "Search repositories"
                  subtitle = "Search Github repositories with \"gh repos {repo-search-term}\""
                  action   = fun _ -> changeQuery "repos" "" };
                { title    = "Search users"
                  subtitle = "Search Github users with \"gh users {user-search-term}\""
                  action   = fun _ -> changeQuery "users" "" } ]

    /// exn -> SearchResult list
    let presentApiSearchExn (e: exn) =
        let defaultResult = { title = "Search failed"; subtitle = e.Message; action = fun _ -> false }
        match e with
        | null ->
            [ defaultResult ]
        | :? Octokit.RateLimitExceededException ->
            [ { defaultResult with
                    title = "Rate limit exceeded"
                    subtitle = "please try again later" } ]
        | :? Octokit.NotFoundException ->
            [ { defaultResult with
                    subtitle = "The repository could not be found" } ]
        | _ ->
            [ defaultResult ]

    let tryRunApiSearch (fSearch: CancellableTask<_>) =
        cancellableTask {
            try
                let! result = fSearch
                return presentApiSearchResult result
            with exn ->
                return presentApiSearchExn exn
        }

    member this.ProcessQuery terms =
        match parseQuery terms with
        | RunApiSearch fSearch -> tryRunApiSearch fSearch
        | SuggestQuery suggestion -> presentSuggestion suggestion |> CancellableTask.singleton

    interface IAsyncPlugin with
        member this.InitAsync(context: PluginInitContext) =
            Helpers.githubTokenFileDir <- context.CurrentPluginMetadata.PluginDirectory

            pluginContext <- context
            Task.CompletedTask

        member this.QueryAsync(query: Query, token: CancellationToken) =
            let ghSearch = cancellableTask {
                let! results =
                    query.SearchTerms
                    |> List.ofArray
                    |> this.ProcessQuery

                return results
                       |> List.map (fun r -> Result( Title = r.title, SubTitle = r.subtitle, IcoPath = "icon.png", Action = fun x -> r.action x ))
                       |> List<Result>
            }
            ghSearch token

    interface IReloadable with
        member this.ReloadData () =
            Cache.clear ()
