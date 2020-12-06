---
layout: post
title: "FsAdvent 2020 - Dependency Injection Using Flexible Types and Type Inference"
published: "2020-12-05"
---

# Dependency Injection Using Flexible Types and Type Inference

## F# Advent 2020

This is a post for [F# Advent 2020](https://sergeytihon.com/2020/10/22/f-advent-calendar-in-english-2020/) facilitated by Sergey Tihon. Visit the link to see many more posts about F#.

## Motivation

When I read [Bartosz Sypytkowski's article on Dealing with complex dependency injection in F#](https://bartoszsypytkowski.com/dealing-with-complex-dependency-injection-in-f/), I knew I had to try out his method. I think his article shows a promising alternative to the "standard" dependency injection approaches you see in C# while using core F# features. This post is about my experience using what he calls an "environment parameter" for dependency injection. In short, I found the experience refreshing, and I am eager to see how the environment parameter handles changes in my application. First, I should explain why "standard" dependency injection is not enough for me.

### .NET Dependency Injection is Boring and Repetitive

The dependency injection I see most often in C# (.NET Core / .NET 5) looks and feels mechanical - use interfaces and instantiate the dependencies at startup yourself or register the interfaces in some dependency injection container. Then, you find out at runtime if you, or your dependency container, missed an interface or implementation. This approach looks like the default way to encapsulate and manage dependencies in .NET with fair reasons - it sounds simple, looks unsurprising (at least before runtime), and C# tooling makes it feel natural. It is boring and repetitive.

Can F# make dependency injection less mechanical for the developer? Can the language figure out what dependencies you need based on how they are used?

If you already read Bartosz's article, you should not be surprised that I think the answer is "yes, probably". The rest of this post will assume you have not read the article, but you really should. If you do read the post, then there will be some questions that sound rhetorical. In this case, try not to roll your eyes too hard. This post is my way of comprehending Bartosz's method.

## What Does F# Offer?

Advocates for F# like to mention the type system, partial application, and type inference. Partial application is a tempting approach, and it seems like an answer to my questions from the previous section. Broadly speaking, you write a function and type inference figures out the types of the arguments and return value based on usage elsewhere in the codebase.

### Partial Application

Unfortunately, I do not think this is less mechanical in practice than the "standard" C# approach.

If you create and use a new dependency, you must add another field or constructor argument to services consuming the new dependency. If an existing dependency needs another capability, you will probably add another parameter and update all services that use this dependency. This feels like something the compiler and type inference can handle for us, but how do we make that happen?

### Flexible Types

Refer to the [F# Language Reference for Flexible Types](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types).

This type annotation allows us to specify that a "a parameter, variable, or value has a type that is compatible with a specified type". My understanding is that this annotation combined with two interfaces is what enables F# type inference to work. Why two interfaces? One interface is for methods tailored to your application logic, and the other interface is to isolate a particular choice of infrastructure (logging, database, some API). Your "environment parameter" will expose the interfaces tailored to your application core logic.

## An Example

I made an internal dotnet cli tool to perform some specific tasks against my company's Stash (Bitbucket) REST API. The cli should apply certain repository permissions sourced from a settings file in a central repository. In other words, the tool supports an infrastructure as code workflow for development teams for their source code repository settings. It was a personal project with simple requirements, so I used it to try out the "environment parameter" approach.

The cli needed a few dependencies: logging, an authenticated http client, and an API to perform the necessary Stash REST API operations. Let's finally see some code trimmed down to show just the environment parameter, so no validation or `Result`.

### Logger Dependency

```fsharp
/// Application will log via these methods.
[<Interface>]
type IAppLogger =
  abstract Debug: string -> unit
  abstract Error: string -> unit

/// env object will use this interface
[<Interface>]
type ILog =
  abstract Logger: IAppLogger

/// #ILog means env can be any type compatible with ILog interface.
/// This is the 'flexible type' annotation and where type inference
/// resolves a compatible interface - it figures out the dependency for us at compile time!
module Log =
  let debug (env: #ILog) fmt =
    Printf.kprintf env.Logger.Debug fmt

  let error (env: #ILog) fmt =
    Printf.kprintf env.Logger.Error fmt

  // Adapt the dependency to IAppLogger.
  // Here I am lazy and log to console, but you can use Microsoft ILogger, NLog, or whatever.
  // if the logger needs configuration, I recommend making any config objects be parameters to `live`.
  let live : IAppLogger =
    { new IAppLogger with
        member _.Debug message = Console.WriteLine ("DEBUG: " + message)
        member _.Error message = Console.WriteLine ("ERROR: " + message) }
```

Next, let's see how a `findUser` function looks that only uses `ILog`.

```fsharp
// val findUser: 
//   env       : ILog   ->
//   searchTerm: string 
//            -> unit
let findUser env = fun searchTerm ->
	Log.debug env "Searching for user with search term: \"%s\"" searchTerm

```

This function does not do anything useful, and the function signature is not surprising. This is just the usual type inference you would expect to see. We need to use another dependency to see an interesting difference in the signature.

### Users API Dependency

Next, let's define the `IStashUsers` and `IStashApi`. If the need for the two logging interfaces was clear, then we can say the two Stash interfaces are analogous to `IAppLogger` and `ILog` interfaces respectively. The first is what the application logic needs, and the second is what the "flexible types" annotation uses to enable the compiler to infer the correct interface and **implicitly** add the dependency to the environment type definition. At least, that is how I understand it. Hopefully not wrong!

```fsharp
// I decided to go perhaps a little too far by isolating the serializer dependency too.
// With System.Text.Json, this may not be remotely useful anymore.
[<Interface>]
type ISerializer =
  abstract Deserialize<'t> : HttpContent -> Async<'t>
  abstract Serialize : 't -> string

module Serializer =
  open Newtonsoft.Json
  open Newtonsoft.Json.Serialization

  let private settings = JsonSerializerSettings()
  settings.ContractResolver <- CamelCasePropertyNamesContractResolver()

  let live =
    { new ISerializer with
        member _.Deserialize<'t> httpContent =
          async {
              let! stringContent = httpContent.ReadAsStringAsync() |> Async.AwaitTask
              let deserialized = JsonConvert.DeserializeObject<'t>(stringContent, settings)
              return deserialized
          }
        member _.Serialize toSerialize =
          JsonConvert.SerializeObject(toSerialize, settings)
    }

[<Interface>]
type IStashUsers =
  abstract GetByUserName: string -> PageResponse<Incoming.UserDto>

[<Interface>]
type IStashApi =
  abstract Users: IStashUsers

module StashUsers =

  let getUserByUserName (env: #IStashApi) searchTerm =
    env.Users.GetByUserName searchTerm

  let live (serializer: ISerializer) stashApiUrl accessToken : IStashUsers =
    { new IStashUsers with
        member _.GetByUserName userName =
          async {
              let! response =
                  FsHttp.DslCE.Builder.httpAsync {
                      GET (sprintf "%s/rest/api/1.0/admin/users?filter=%s" stashApiUrl (Http.encode userName))
                      Authorization (sprintf "Bearer %s" accessToken)
                  }

              return! serializer.Deserialize<PageResponse<Incoming.UserDto>> response.content response
          }
    }

```

### Using Two Dependencies Together

Notice how `env` changed to require both `ILog` and `IStashApi` once `findUser` uses `Log.debug` and `StashUsers.getUserByUserName`. Again, this type inference works because the `Log` and `StashUsers` modules use the `#ILog` and `#IStashApi` flexible type annotations respectively.

```fsharp

// val findUser: 
//    env       : 'a     (requires :> ILog and :> IStashApi )->
//    searchTerm: string 
//             -> option<UserDto>
let findUser env = fun searchTerm ->
  Log.debug env "Searching for user with search term: \"%s\"" searchTerm

  // PageResponse<UserDto>
  let x = StashUsers.getUserByUserName env searchTerm

  // option<UserDto>
  let user = x.Values |> Array.tryHead

  Log.debug env "Best match for %s is %s" searchTerm user.Name
  
  user

```

## Does Environment Parameter Answer My Questions?

The questions were:
- Can F# make dependency injection less mechanical for the developer?
- Can the language figure out what dependencies you need based on how they are used?

I think the answer is **yes, probably**.

If I take away all uses of the `Log` module from `findUser` then `env` type signature is only `IStashApi`.

If I create a third module `SomeOtherDependency` following the same two interface pattern with `#ISomeOtherDependency` flexible type annotation pattern and use that module in `findUser`, then `env` will automatically be inferred to require the third interface. Pretty convenient!

I do not depend on some library or framework. Type inference and flexible type annotations are standard F# language features. If the environment type does not meet the needs of some function in some module, the code will not compile.

You still need to provide proper configurations, connection strings, etc at startup. The compiler does not check that, unless you are willing to add in a type provider. [SQLProvider](https://fsprojects.github.io/SQLProvider/) for example checks queries against a real database at compile time. Maybe there is a type provider or similar tool to do that for your configured dependency? That does not sound worth the effort and is beyond the scope of this post.

### Remaining Questions

So far this post sounds like I am totally sold and have no other concerns. That is not true. I have some unanswered and untested questions.

- How to handle service lifetime and scoping, if at all?
- Can this approach be accomplished in C#?
	- Perhaps by using [type constraints](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters), but I think C# would need type inference. No idea.
- Is this easier than "standard" C# Microsoft.Extensions.DependencyInjection?
	- I think so, but my application is still simple compared to other codebases I work with.

## Links and Contact

View the other [F# Advent 2020 posts](https://sergeytihon.com/2020/10/22/f-advent-calendar-in-english-2020/)!

I would like to thank Bartosz for his post. I think it showed me a middle ground between partial application and a reader monad that I would not have found by myself.

Links:
* [Dealing with complex dependency injection in F# - Bartosz Sypytkowski, 22-Mar-2020](https://bartoszsypytkowski.com/dealing-with-complex-dependency-injection-in-f/)
* [F# Language Reference: Flexible Types](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types)

Contact:

I do not have a comments section, so please use [@garthfritz on Twitter](https://twitter.com/garthfritz/) or `@garth` on the [F# Software Foundation Slack (slack access requires free F# Software Foundation membership)](https://fsharp.org/guides/slack/) to contact me with feedback or clarification.
