module Flow.Plugin.Github.Tests

open Expecto
open Expecto.Flip
open Flow.Plugin.Github
open System.Threading
open Flow.Launcher.Plugin

let plugin = GithubPlugin()

let allTests =
    testList "all tests" [

        testTask "default query suggestions" {
            let! results = plugin.ProcessQuery [] CancellationToken.None
            results |> List.length |> Expect.equal "there should be two suggestions" 2
        }

        testTask "search suggestions" {
            // should return search suggestions
            let! (results: Result list) = plugin.ProcessQuery [ "some-search-term" ] CancellationToken.None
            let result1 = results |> List.tryItem 0 |> Expect.wantSome "result 1 should exist"
            let result2 = results |> List.tryItem 1 |> Expect.wantSome "result 2 should exist"

            result1.Title    |> Expect.equal        "result 1 title should match"         "Search repositories"
            result1.SubTitle |> Expect.stringStarts "result 1 subtitle should start with" "Search for repositories matching"
            result2.Title    |> Expect.equal        "result 2 title should match"         "Search users"
            result2.SubTitle |> Expect.stringStarts "result 2 subtitle should start with" "Search for users matching"
        }

        testTask "empty repo search" {
            // should not return a "Search failed" error
            let! (results: Result list) = plugin.ProcessQuery [ "repos" ] CancellationToken.None

            let result = results |> List.tryHead |> Expect.wantSome "there should be one result"

            result.Title |> Expect.equal "title should match" "Search Github"
        }

        testTask "repo search" {
            // should return a list of repositories
            let! (results: Result list) = plugin.ProcessQuery [ "repos"; "wox" ] CancellationToken.None
            
            results |> Expect.isNonEmpty "should not be empty"
        }

        testTask "repo search with spaces" {
            let! (results1: Result list) = plugin.ProcessQuery [ "repos"; "launcher"; "flow" ] CancellationToken.None
            let! (results2: Result list) = plugin.ProcessQuery [ "repos"; "launcher"; "wox" ] CancellationToken.None

            let res1 = results1 |> List.tryHead |> Expect.wantSome "first query should return at least one result"
            let res2 = results2 |> List.tryHead |> Expect.wantSome "second query should return at least one result"

            (res1.Title,    res2.Title)    ||> Expect.notEqual "titles should not match"
            (res1.SubTitle, res2.SubTitle) ||> Expect.notEqual "subtitles should not match"
        }

        testTask "empty user search" {
            // should not return a "Search failed" error
            let! (results: Result list) = plugin.ProcessQuery [ "users" ] CancellationToken.None

            let result = results |> List.tryHead |> Expect.wantSome "there should be one result"

            result.Title |> Expect.equal "title should match" "Search Github"
        }

        testTask "user search" {
            // should return a list of users
            let! res = plugin.ProcessQuery [ "users"; "john" ] CancellationToken.None
            
            res |> Expect.isNonEmpty "should not be empty"
        }

        testTask "user repo search" {
            // should return a list of repositories owned by wox-launcher
            let! (results: Result list) = plugin.ProcessQuery [ "wox-launcher/" ] CancellationToken.None
            
            results |> Expect.isNonEmpty "should not be empty"

            for result in results do
                result.Title |> Expect.stringStarts "title should start with" "Wox-launcher/"
        }

        testTask "repo issues format" {
            // should return a list of issues
            let! (results: Result list) = plugin.ProcessQuery [ "issues"; "wox-launcher/wox" ] CancellationToken.None
            for result in results do
                result.SubTitle |> Expect.stringStarts "subtitle should start with" "issue #"
        }

        testTask "repo PRs format" {
            // should return a list of PRs
            let! (results: Result list) = plugin.ProcessQuery [ "pull"; "git/git" ] CancellationToken.None
            for result in results do
                result.SubTitle |> Expect.stringStarts "subtitle should start with" "PR #"
        }

        testTask "single repo issue format" {
            // should return a single issue
            let! (results: Result list) = 
                plugin.ProcessQuery [ "wox-launcher/wox"; "#977" ] CancellationToken.None
                
            let testResult =
                results
                |> List.tryExactlyOne
                |> Expect.wantSome "there should be one result"
            
            testResult.Title |> Expect.equal "title should match" "#977 - Highlighting how results matched"
            testResult.SubTitle |> Expect.stringStarts "subtitle should start with" "closed | created by JohnTheGr8 | last updated"
        }

        testTask "repo details format" {
            // should return stats/issues/PRs
            let! (results: Result list) = plugin.ProcessQuery [ "repo"; "wox-launcher/wox" ] CancellationToken.None

            results.Length       |> Expect.equal "should have exactly 3 results" 3
            results.[0].Title    |> Expect.isNotEmpty "result 1 title should not be empty"
            results.[0].SubTitle |> Expect.isNotEmpty "result 1 subtitle should not be empty"
            results.[1].SubTitle |> Expect.stringEnds "result 2 subtitle should end with" "issues open"
            results.[2].SubTitle |> Expect.stringEnds "result 2 subtitle should end with" "pull requests open"
        }

        testTask "repo details alt format" {
            // should return stats/issues/PRs
            let! (results1: Result list) = plugin.ProcessQuery [ "wox-launcher/wox" ] CancellationToken.None
            let! (results2: Result list) = plugin.ProcessQuery [ "repo"; "wox-launcher/wox" ] CancellationToken.None

            for result1, result2 in List.zip results1 results2 do
                (result1.Title, result2.Title)       ||> Expect.equal "titles should be equal"
                (result1.SubTitle, result2.SubTitle) ||> Expect.equal "subtitles should be equal"
        }

        testList "bad searches" [

            testTask "invalid repo details" {
                let! (results: Result list) = 
                    plugin.ProcessQuery [ "repo"; "repothat/doesntexist" ] CancellationToken.None
                    
                let testResult = 
                    results
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.Title |> Expect.equal "title should equal" "Search failed"
            }

            testTask "invalid repo issues" {
                let! (results: Result list) = 
                    plugin.ProcessQuery [ "issues"; "repothat/doesntexist" ] CancellationToken.None
                    
                let testResult = 
                    results
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.Title |> Expect.equal "title should equal" "Search failed"
            }

            testTask "invalid repo PRs" {
                let! (results: Result list) = 
                    plugin.ProcessQuery [ "pr"; "repothat/doesntexist" ] CancellationToken.None
                  
                let testResult = 
                    results
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.Title |> Expect.equal "title should equal" "Search failed"
            }

            testTask "invalid repo search" {
                let! (results: Result list) = 
                    plugin.ProcessQuery [ "repos"; "repothatdoesntexist" ] CancellationToken.None
                    
                let testResult = 
                    results
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.Title |> Expect.equal "title should equal" "No results found"
            }

            testTask "invalid user search" {
                let! (results: Result list) = 
                    plugin.ProcessQuery [ "users"; "userthatdoesntexist" ] CancellationToken.None

                let testResult = 
                    results
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.Title |> Expect.equal "title should equal" "No results found"
            }
        ]
    ]

[<EntryPoint>]
let main argv =
    Tests.runTestsWithArgs defaultConfig argv allTests
