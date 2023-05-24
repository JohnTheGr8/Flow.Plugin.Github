module Flow.Plugin.Github.Tests

open Expecto
open Expecto.Flip
open Flow.Plugin.Github
open System.Threading

let plugin = GithubPlugin()

let allTests =
    testList "all tests" [

        testTask "default query suggestions" {
            let! results = plugin.ProcessQuery [] CancellationToken.None
            results |> List.length |> Expect.equal "there should be two suggestions" 2
        }

        testTask "search suggestions" {
            // should return search suggestions
            let! results = plugin.ProcessQuery [ "some-search-term" ] CancellationToken.None
            let result1 = results |> List.tryItem 0 |> Expect.wantSome "result 1 should exist"
            let result2 = results |> List.tryItem 1 |> Expect.wantSome "result 2 should exist"

            result1.title    |> Expect.equal        "result 1 title should match"         "Search repositories"
            result1.subtitle |> Expect.stringStarts "result 1 subtitle should start with" "Search for repositories matching"
            result2.title    |> Expect.equal        "result 2 title should match"         "Search users"
            result2.subtitle |> Expect.stringStarts "result 2 subtitle should start with" "Search for users matching"
        }

        testTask "empty repo search" {
            // should not return a "Search failed" error
            let! results = plugin.ProcessQuery [ "repos" ] CancellationToken.None

            let result = results |> List.tryHead |> Expect.wantSome "there should be one result"

            result.title |> Expect.equal "title should match" "Search Github"
        }

        testTask "repo search" {
            // should return a list of repositories
            let! res = plugin.ProcessQuery [ "repos"; "wox" ] CancellationToken.None
            
            res |> Expect.isNonEmpty "should not be empty"
        }

        testTask "repo search with spaces" {
            let! results1 = plugin.ProcessQuery [ "repos"; "launcher"; "flow" ] CancellationToken.None
            let! results2 = plugin.ProcessQuery [ "repos"; "launcher"; "wox" ] CancellationToken.None

            let res1 = results1 |> List.tryHead |> Expect.wantSome "first query should return at least one result"
            let res2 = results2 |> List.tryHead |> Expect.wantSome "second query should return at least one result"

            (res1.title,    res2.title)    ||> Expect.notEqual "titles should not match"
            (res1.subtitle, res2.subtitle) ||> Expect.notEqual "subtitles should not match"
        }

        testTask "empty user search" {
            // should not return a "Search failed" error
            let! results = plugin.ProcessQuery [ "users" ] CancellationToken.None

            let result = results |> List.tryHead |> Expect.wantSome "there should be one result"

            result.title |> Expect.equal "title should match" "Search Github"
        }

        testTask "user search" {
            // should return a list of users
            let! res = plugin.ProcessQuery [ "users"; "john" ] CancellationToken.None
            
            res |> Expect.isNonEmpty "should not be empty"
        }

        testTask "user repo search" {
            // should return a list of repositories owned by wox-launcher
            let! results = plugin.ProcessQuery [ "wox-launcher/" ] CancellationToken.None
            
            results |> Expect.isNonEmpty "should not be empty"

            for result in results do
                result.title |> Expect.stringStarts "title should start with" "Wox-launcher/"
        }

        testTask "repo issues format" {
            // should return a list of issues
            let! results = plugin.ProcessQuery [ "issues"; "wox-launcher/wox" ] CancellationToken.None
            for result in results do
                result.subtitle |> Expect.stringStarts "subtitle should start with" "issue #"
        }

        testTask "repo PRs format" {
            // should return a list of PRs
            let! results = plugin.ProcessQuery [ "pull"; "git/git" ] CancellationToken.None
            for result in results do
                result.subtitle |> Expect.stringStarts "subtitle should start with" "PR #"
        }

        testTask "single repo issue format" {
            // should return a single issue
            let! result = 
                plugin.ProcessQuery [ "wox-launcher/wox"; "#977" ] CancellationToken.None
                
            let testResult = 
                result
                |> List.tryExactlyOne
                |> Expect.wantSome "there should be one result"
            
            testResult.title |> Expect.equal "title should match" "#977 - Highlighting how results matched"
            testResult.subtitle |> Expect.stringStarts "subtitle should start with" "closed | created by JohnTheGr8 | last updated"
        }

        testTask "repo details format" {
            // should return stats/issues/PRs
            let! (results: _ list) = plugin.ProcessQuery [ "repo"; "wox-launcher/wox" ] CancellationToken.None

            results.Length       |> Expect.equal "should have exactly 3 results" 3
            results.[0].title    |> Expect.isNotEmpty "result 1 title should not be empty"
            results.[0].subtitle |> Expect.isNotEmpty "result 1 subtitle should not be empty"
            results.[1].subtitle |> Expect.stringEnds "result 2 subtitle should end with" "issues open"
            results.[2].subtitle |> Expect.stringEnds "result 2 subtitle should end with" "pull requests open"
        }

        testTask "repo details alt format" {
            // should return stats/issues/PRs
            let! results1 = plugin.ProcessQuery [ "wox-launcher/wox" ] CancellationToken.None
            let! results2 = plugin.ProcessQuery [ "repo"; "wox-launcher/wox" ] CancellationToken.None

            for result1, result2 in List.zip results1 results2 do
                (result1.title, result2.title)       ||> Expect.equal "titles should be equal"
                (result1.subtitle, result2.subtitle) ||> Expect.equal "subtitles should be equal"
        }

        testList "bad searches" [

            testTask "invalid repo details" {
                let! result = 
                    plugin.ProcessQuery [ "repo"; "repothat/doesntexist" ] CancellationToken.None
                    
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "Search failed"
            }

            testTask "invalid repo issues" {
                let! result = 
                    plugin.ProcessQuery [ "issues"; "repothat/doesntexist" ] CancellationToken.None
                    
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "Search failed"
            }

            testTask "invalid repo PRs" {
                let! result = 
                    plugin.ProcessQuery [ "pr"; "repothat/doesntexist" ] CancellationToken.None
                  
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "Search failed"
            }

            testTask "invalid repo search" {
                let! result = 
                    plugin.ProcessQuery [ "repos"; "repothatdoesntexist" ] CancellationToken.None
                    
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "No results found"
            }

            testTask "invalid user search" {
                let! result = 
                    plugin.ProcessQuery [ "users"; "userthatdoesntexist" ] CancellationToken.None
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "No results found"
            }
        ]
    ]

[<EntryPoint>]
let main argv =
    Tests.runTestsWithArgs defaultConfig argv allTests
