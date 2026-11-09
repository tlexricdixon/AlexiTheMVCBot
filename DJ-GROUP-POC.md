# DJ group rehearsal (development-only proof of concept)

Run the Blazor app in the **Development** environment and visit `/group`.
Click **Let the DJs talk (3 turns)** to create three sequential character replies in one
shared in-memory studio transcript. Click again to advance to other DJs, or **Start over**
to clear the experiment.

- All eight DJ personas are reused from `DjProfiles`, with a short group-specific instruction.
- The last 12 room messages are supplied to the responding DJ as one quoted transcript.
- Three turns are generated per user click; no autonomous loop or guest message endpoint.
- One server-wide `DjGroupRoom` coordinates requests; a semaphore prevents overlapping runs.
- The in-memory transcript is bounded to 32 messages. It resets on process restart.
- The demo is disabled outside Development, even if the route is reachable.
- Ollama requests ask to keep the model in memory for 30 minutes; they use the configured
  model/BaseUrl and a 4,096-token context with a 100-token reply limit.
- Existing private per-circuit chat remains unchanged.

## Manual verification

1. Start Ollama with the configured model available; launch the app in Development.
2. Open `/group` in two browser windows. Confirm both show the same room history.
3. Click the generate button. Confirm three speaker turns appear and the second
   browser receives the updates. Click again and verify the next three distinct DJs
   participate; after eight turns the speaker order wraps.
4. Verify new replies reference earlier DJs rather than acting as isolated greetings.
5. Reset, verify both windows clear, and run again.
6. Stop Ollama and verify the page shows a generic failure, not internal error details.
7. Run with `ASPNETCORE_ENVIRONMENT=Production`; verify generation is disabled.

This is an experimental branch: a successful build, manual checks, and actual
multi-browser testing are still required before merging or hosting.

## Next work (not included)

- **Public listener lounge**: moderated guest messages, unique guest IDs, server-side
  abuse throttling, bounded Ollama queue, and a deliberate DJ response policy.
- **Persistence**: PostgreSQL tables for messages, room membership/identities,
  response metadata, moderation actions, and visibility. Provide a restricted review
  interface for checking AI behavior before letting untrusted visitors chat.
- **Later: private chat**: email-login identity and independently stored private
  conversations; no private history may appear in the public room. Defer implementing
  email authentication until explicitly requested.
