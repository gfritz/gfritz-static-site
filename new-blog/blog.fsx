#r "paket: groupref blognetcore //"
#load ".fake/blog.fsx/intellisense.fsx"

#if !FAKE
  #r ".fake/blog.fsx/NETStandard.Library.NETFramework/build/netstandard2.0/ref/netstandard.dll"
#endif

#nowarn "52"

open Fable.Helpers.React
open Fable.Helpers.React.Props
open FSharp.Markdown
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

type Post = {
  Title : string
  Content : string
}

let template post =
  html [Lang "en"] [
    head [] [
      title [] [ str ("Garth Blog / " + post.Title) ]
    ]
    body [] [
      RawText post.Content
    ]
  ]

let render html =
  fragment [] [
    RawText "<!doctype html>"
    RawText "\n"
    html ]
  |> Fable.Helpers.ReactServer.renderToString

let posts postsGlobbingPattern =
  !! postsGlobbingPattern
  |> Seq.map (fun f ->
      { Title = File.readLine f |> (fun firstLine -> firstLine.Replace("#", System.String.Empty).Trim() ) // first line should be the title
        Content = File.readAsString f }
  )

let renderPostToHtml post =
  post |> template |> render

let writePostsToStagingDirectory stagingDirectory renderFunc posts =
  Directory.ensure stagingDirectory

  posts
  |> Seq.iter (fun p -> printfn "%s %s" (stagingDirectory </> p.Title) (renderFunc p))
  // |> Seq.iter (fun p -> File.writeString false (stagingDirectory </> p.Title) (renderFunc p))

posts "posts/*.md"
|> writePostsToStagingDirectory "staging" renderPostToHtml