#r "packages/Suave/lib/net40/Suave.dll"
open System
open System.IO
open Suave
open Suave.Web
open Suave.Http
open Suave.Successful
open Suave.RequestErrors
open Suave.Operators
open Suave.Filters

// ------------------------------------------------------------------

// TODO: What is the ChatMessage that chat agent handles?
// TODO: Implement chat agent to store the room state
// (Format messages as "<li><strong>%s</strong>: %s</li>")

type Message =
  | Add of string * String
  | Get of AsyncReplyChannel<string>

let chat = MailboxProcessor.Start(fun inbox ->
  let rec loop msgs = async {
    let! msg = inbox.Receive()
    match msg with
    | Add(n, t) -> return! loop ((n, t)::msgs)
    | Get(repl) ->
        [ for n, t in msgs -> sprintf "<li><strong>%s</strong>: %s</li>" n t ]
        |> String.concat ""
        |> repl.Reply
        return! loop msgs  }
  loop [] )

// ------------------------------------------------------------------

let getMessages room ctx = async {
  let body = chat.PostAndReply(Get)
  let html = "<ul>" + body + "</ul>"
  return! OK html ctx }

let postMessage room ctx = async {
  let name = ctx.request.url.Query.Substring(1)
  use sr = new StreamReader(new MemoryStream(ctx.request.rawForm))
  let text = sr.ReadToEnd()
  chat.Post(Add(name, text))
  return! ACCEPTED "OK" ctx }

// DEMO: Add handlers for REST API
// TODO: Handle /chat with GET & no chache using getMessage
// TODO: Handle /post with POST & no cache using postMessage
// TODO: Otherwise, report NOT_FOUND

let index = File.ReadAllText(__SOURCE_DIRECTORY__ + "/web/chat.html")

let noCache =
  Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
  >=> Writers.setHeader "Pragma" "no-cache"
  >=> Writers.setHeader "Expires" "0"

let app =
  choose
    [ path "/" >=> Writers.setMimeType "text/html" >=> OK index
      path "/chat" >=> GET >=> noCache >=> getMessages "Home"
      path "/post" >=> POST >=> noCache >=> postMessage "Home"
      NOT_FOUND "ooops!" ]
