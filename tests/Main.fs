module Flow.Plugin.Github.Tests

open Expecto
open Expecto.Flip
open Flow.Plugin.Github

let plugin = GithubPlugin()

let allTests =
    testList "all tests" [

        testAsync "default query suggestions" {
            let! results = plugin.ProcessQuery []
            results.Length |> Expect.equal "there should be two suggestions" 2
        }

        testAsync "search suggestions" {
            // should return search suggestions
            let! results = plugin.ProcessQuery [ "some-search-term"; "" ]
            let result1 = results |> List.tryItem 0 |> Expect.wantSome "result 1 should exist"
            let result2 = results |> List.tryItem 1 |> Expect.wantSome "result 2 should exist"

            result1.title    |> Expect.equal        "result 1 title should match"         "Search repositories"
            result1.subtitle |> Expect.stringStarts "result 1 subtitle should start with" "Search for repositories matching"
            result2.title    |> Expect.equal        "result 2 title should match"         "Search users"
            result2.subtitle |> Expect.stringStarts "result 2 subtitle should start with" "Search for users matching"
        }

        testAsync "repo search" {
            // should return a list of repositories
            let! res = plugin.ProcessQuery [ "repos"; "wox" ]
            
            res|>
            Expect.isNonEmpty "should not be empty"
        }

        testAsync "user search" {
            // should return a list of users
            let! res = plugin.ProcessQuery [ "users"; "john" ]
            
            res |>
            Expect.isNonEmpty "should not be empty"
        }

        testAsync "user repo search" {
            // should return a list of repositories owned by wox-launcher
            let! results = plugin.ProcessQuery [ "wox-launcher/"; "" ]
            
            results |> Expect.isNonEmpty "should not be empty"

            for result in results do
                result.title |> Expect.stringStarts "title should start with" "Wox-launcher/"
        }

        testAsync "repo issues format" {
            // should return a list of issues
            let! results = plugin.ProcessQuery [ "issues"; "wox-launcher/wox" ]
            for result in results do
                result.subtitle |> Expect.stringStarts "subtitle should start with" "issue #"
        }

        testAsync "repo PRs format" {
            // should return a list of PRs
            let! results = plugin.ProcessQuery [ "pull"; "git/git" ]
            for result in results do
                result.subtitle |> Expect.stringStarts "subtitle should start with" "PR #"
        }

        testAsync "single repo issue format" {
            // should return a single issue
            let! result = 
                plugin.ProcessQuery [ "wox-launcher/wox"; "#977" ]
                
            let testResult = 
                result
                |> List.tryExactlyOne
                |> Expect.wantSome "there should be one result"
            
            testResult.title |> Expect.equal "title should match" "#977 - Highlighting how results matched"
            testResult.subtitle |> Expect.stringStarts "subtitle should start with" "closed | created by JohnTheGr8 | last updated"
        }

        testAsync "repo details format" {
            // should return stats/issues/PRs
            let! results = plugin.ProcessQuery [ "repo"; "wox-launcher/wox" ]
            results.Length       |> Expect.equal "should have exactly 3 results" 3
            results.[0].title    |> Expect.isNotEmpty "result 1 title should not be empty"
            results.[0].subtitle |> Expect.isNotEmpty "result 1 subtitle should not be empty"
            results.[1].subtitle |> Expect.stringEnds "result 2 subtitle should end with" "issues open"
            results.[2].subtitle |> Expect.stringEnds "result 2 subtitle should end with" "pull requests open"
        }

        testAsync "repo details alt format" {
            // should return stats/issues/PRs
            let! results1 = plugin.ProcessQuery [ "wox-launcher/wox"; "" ]
            let! results2 = plugin.ProcessQuery [ "repo"; "wox-launcher/wox" ]

            for result1, result2 in List.zip results1 results2 do
                (result1.title, result2.title)       ||> Expect.equal "titles should be equal"
                (result1.subtitle, result2.subtitle) ||> Expect.equal "subtitles should be equal"
        }

        testList "bad searches" [

            testAsync "invalid repo details" {
                let! result = 
                    plugin.ProcessQuery [ "repo"; "repothat/doesntexist" ]
                    
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "Search failed"
            }

            testAsync "invalid repo issues" {
                let! result = 
                    plugin.ProcessQuery [ "issues"; "repothat/doesntexist" ]
                    
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "Search failed"
            }

            testAsync "invalid repo PRs" {
                let! result = 
                    plugin.ProcessQuery [ "pr"; "repothat/doesntexist" ]
                  
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "Search failed"
            }

            testAsync "invalid repo search" {
                let! result = 
                    plugin.ProcessQuery [ "repos"; "repothatdoesntexist" ]
                    
                let testResult = 
                    result
                    |> List.tryExactlyOne
                    |> Expect.wantSome "there should be one result"
                
                testResult.title |> Expect.equal "title should equal" "No results found"
            }

            testAsync "invalid user search" {
                let! result = 
                    plugin.ProcessQuery [ "users"; "userthatdoesntexist" ]
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
