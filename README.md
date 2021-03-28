Flow.Plugin.Github [![Build status](https://ci.appveyor.com/api/projects/status/6gonqqny035188wj?svg=true)](https://ci.appveyor.com/project/JohnTheGr8/flow-plugin-github)
==================

Github plugin for the [Flow launcher](https://github.com/Flow-Launcher/Flow.Launcher)

### About

Search Github repositories, navigate repository issues and pull requests, directly from Wox.

![demo gif](https://i.imgur.com/kHGbBQI.gif)

### Usage

Search for repos: `` gh repos {repo-search-term} ``

Display repository info: `` gh repo {full-repo-name} ``

List repository issues: `` gh issues {full-repo-name} ``

List repository pull requests: `` gh pr {full-repo-name} ``

Search for users: `` gh users {user-search-term} ``

Find specific issue or pull request: `` gh {full-repo-name} #123 ``

List repositories by user: `` gh {owner-name}/ ``

### Access Token

To avoid rate limits from Github's API, after installing the plugin do the following:

1. open Github and [generate a new personal access token](https://github.com/settings/tokens/new)
2. open the plugin's directory, create a `github_token.txt` file and add the API token
3. restart Wox

### Private Repositories

Simply check the `repo` scope when generating the access token.

### Credits

- [octokit.net](https://github.com/octokit/octokit.net) : A GitHub API client library for .NET
- [expecto](https://github.com/haf/expecto) : testing library
- [humanizer](https://github.com/Humanizr/Humanizer) : Library used to turn date-times into a relative format
- [Github Icon](https://www.iconfinder.com/icons/291716/github_logo_social_social_network_icon) : Icon used
