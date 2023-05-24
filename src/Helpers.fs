[<AutoOpen>]
module Flow.Plugin.Github.Helpers

open System
open System.IO
open System.Text.RegularExpressions

let (|UserRepoFormat|_|) (name:string) =
    let m = Regex.Match(name, "^(?<user>[\w\.-]+)(\/)(?<repo>[\w\.-]+)$")
    if m.Success
    then Some (m.Groups.["user"].Value, m.Groups.["repo"].Value)
    else None

let (|IssueFormat|_|) (value: string) =
    if value.StartsWith "#" && value.Length > 1 then
        match Int32.TryParse (value.Substring 1) with
        | true, x when x > 0 -> Some x
        | _ -> None
    else None

let (|UserReposFormat|_|) (value: string) = 
    if value.EndsWith "/" && value.Length > 1 then
        value.Substring (0, value.Length - 1) |> Some
    else
        None

let (|RepoIssuesFormat|_|) (value: string) = 
    let m = Regex.Match(value, "^(?<user>[\w\.-]+)(\/)(?<repo>[\w\.-]+)\/issues$")
    if m.Success
    then Some (m.Groups.["user"].Value, m.Groups.["repo"].Value)
    else None

let (|RepoPullsFormat|_|) (value: string) = 
    let m = Regex.Match(value, "^(?<user>[\w\.-]+)(\/)(?<repo>[\w\.-]+)\/pulls$")
    if m.Success
    then Some (m.Groups.["user"].Value, m.Groups.["repo"].Value)
    else None

let (|UserRepoIssueFormat|_|) (value: string) =
    let m = Regex.Match(value, "^(?<user>[\w\.-]+)(\/)(?<repo>[\w\.-]+)(#|\/(issue|pull)\/)(?<issue>\d+)$")
    if m.Success
    then Some (m.Groups.["user"].Value, m.Groups.["repo"].Value, int m.Groups.["issue"].Value)
    else None

let (|CompleteQuery|IncompleteQuery|BadQuery|) = function
    | "repos" :: []                               -> IncompleteQuery
    | "users" :: []                               -> IncompleteQuery
    | "repos" :: search                           -> CompleteQuery (FindRepos (String.concat " " search))
    | "users" :: search                           -> CompleteQuery (FindUsers (String.concat " " search))
    | "issues" :: UserRepoFormat search :: []     -> CompleteQuery (FindIssues search)
    | "pr"     :: UserRepoFormat search :: []     -> CompleteQuery (FindPRs search)
    | "pull"   :: UserRepoFormat search :: []     -> CompleteQuery (FindPRs search)
    | "repo"   :: UserRepoFormat search :: []     -> CompleteQuery (FindRepo search)
    | UserRepoFormat search :: []                 -> CompleteQuery (FindRepo search)
    | RepoIssuesFormat search :: []               -> CompleteQuery (FindIssues search)
    | UserRepoFormat search :: "issues" :: []     -> CompleteQuery (FindIssues search)
    | RepoPullsFormat  search :: []               -> CompleteQuery (FindPRs search)
    | UserRepoFormat search :: "pr"     :: []     -> CompleteQuery (FindPRs search)
    | UserRepoFormat search :: "pull"   :: []     -> CompleteQuery (FindPRs search)
    | UserRepoIssueFormat (u,r,i)           :: [] -> CompleteQuery (FindIssueOrPr (u,r,i))
    | UserRepoFormat (u,r) :: IssueFormat i :: [] -> CompleteQuery (FindIssueOrPr (u,r,i))
    | UserReposFormat user :: []                  -> CompleteQuery (FindUserRepos user)
    | search :: []                                -> BadQuery (SearchRepos search)
    | _                                           -> BadQuery DefaultSuggestion

let toLower (s: string) = s.ToLower()

let tryEnvVar var =
    match Environment.GetEnvironmentVariable var with
    | null -> None
    | value -> Some value

let mutable githubTokenFileDir = __SOURCE_DIRECTORY__

let tryReadFile fileName =
    let path = Path.Combine(githubTokenFileDir, fileName)
    if File.Exists path then File.ReadAllText path |> Some else None

let tryLoadGithubToken () = 
    tryEnvVar "GITHUB_API_TOKEN"
    |> Option.orElse (tryReadFile "github_token.txt")
