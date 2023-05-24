[<AutoOpen>]
module Flow.Plugin.Github.Helpers

open System
open System.IO
open System.Text.RegularExpressions

let (|UserRepoFormat|_|) (name:string) =
    let m = Regex.Match(name, "^(?<user>(.+))(\/)(?<repo>(.+))$")
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

let (|CompleteQuery|BadQuery|) = function
    | "repos" :: search                           -> CompleteQuery (FindRepos (String.concat " " search))
    | "users" :: search                           -> CompleteQuery (FindUsers (String.concat " " search))
    | "issues" :: UserRepoFormat search :: []     -> CompleteQuery (FindIssues search)
    | "pr"     :: UserRepoFormat search :: []     -> CompleteQuery (FindPRs search)
    | "pull"   :: UserRepoFormat search :: []     -> CompleteQuery (FindPRs search)
    | "repo"   :: UserRepoFormat search :: []     -> CompleteQuery (FindRepo search)
    | UserRepoFormat search :: []                 -> CompleteQuery (FindRepo search)
    | UserRepoFormat search :: "issues" :: []     -> CompleteQuery (FindIssues search)
    | UserRepoFormat search :: "pr"     :: []     -> CompleteQuery (FindPRs search)
    | UserRepoFormat search :: "pull"   :: []     -> CompleteQuery (FindPRs search)
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
