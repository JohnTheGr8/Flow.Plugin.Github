namespace Flow.Plugin.Github

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
    | Repos of Octokit.Repository list
    | RepoIssues of Octokit.Issue list
    | RepoIssueOrPr of Octokit.Issue
    | RepoPRs of Octokit.Issue list
    | Users of Octokit.User list
    | RepoDetails of Octokit.Repository * Octokit.Issue list * Octokit.Issue list

type QuerySuggestion =
    | SearchRepos of string
    | DefaultSuggestion
