Flow.Plugin.Github [![Build status](https://ci.appveyor.com/api/projects/status/6gonqqny035188wj?svg=true)](https://ci.appveyor.com/project/JohnTheGr8/flow-plugin-github)
==================

Github plugin for the [Flow launcher](https://github.com/Flow-Launcher/Flow.Launcher)

### About

Search Github repositories, navigate repository issues and pull requests, directly from Flow-Launcher.

![demo gif](https://i.imgur.com/kHGbBQI.gif)

### Usage

> note: The plugin supports many sub-query formats, use whatever suits you best...

Search for repos: 
* `` gh repos {repo-search-term} ``

Search for users: 
* `` gh users {user-search-term} ``

List repositories by user: 
* `` gh {owner}/ ``

Display repository info: 
* `` gh {owner}/{repo} ``

List repository issues: 
* `` gh {owner}/{repo}/issues ``
* `` gh {owner}/{repo} issues ``
* `` gh issues {owner}/{repo} ``

List repository pull requests:
* `` gh {owner}/{repo}/pulls ``
* `` gh {owner}/{repo} pull ``
* `` gh {owner}/{repo} pr ``
* `` gh pull {owner}/{repo} ``
* `` gh pr {owner}/{repo} ``

Find specific issue or pull request: 
* `` gh {owner}/{repo}#123 ``
* `` gh {owner}/{repo} #123 ``
* `` gh {owner}/{repo}/issue/123 ``
* `` gh {owner}/{repo}/pull/123 ``

### Access Token

To avoid rate limits from Github's API, after installing the plugin do the following:

1. open Github and [generate a new personal access token](https://github.com/settings/tokens/new)
2. hit `Enter` on the Rate Limit result, or manually open `%AppData%\FlowLauncher\Settings\Plugins\Flow.Plugin.Github\Settings.json`
3. add your token in the `GithubApiToken` value and save the file

### Private Repositories

Simply check the `repo` scope when generating the access token.

### Credits

- [octokit.net](https://github.com/octokit/octokit.net) : A GitHub API client library for .NET
- [expecto](https://github.com/haf/expecto) : testing library
- [humanizer](https://github.com/Humanizr/Humanizer) : Library used to turn date-times into a relative format
- [Github Icon](https://www.iconfinder.com/icons/291716/github_logo_social_social_network_icon) : Icon used
