# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Presenter (host):** runs the quiz from `/admin` on a laptop, a TV, or a projector. Clicks Start / Next and reads the room. Screen must read from across a room in a lit living room and in a dimmed event hall.
- **Players:** friends, or colleagues at a company party. Join on their own phones at `/game`, usually standing or sitting in a group, glancing between the big screen and the phone. Mixed ages and tech comfort; nobody installs anything or reads instructions.

## Product Purpose

A live, Kahoot-style party quiz for one room. The host shows questions on the big screen; everyone answers on their phone at the same time; scores, speed and a running leaderboard keep it competitive. Success: people laugh, shout at the screen, want "one more round", and nobody gets lost in the controls.

## Positioning

A private, home-made quiz the host writes themselves (static `Quiz/quiz.json`): four question types (pick one of four, true/false, match pairs, closest number), a speed bonus, hand-drawn player faces as avatars, and encouraging rank messages on each phone after every question.

## Operating Context

- Flow: lobby (players join, draw a face) → "The fun begins now!" 5-second countdown → question with 30 s timer (45 s for matching), last 5 s enlarged and red → reveal → overall top-5 leaderboard → next question → final podium with medals.
- Big screen and phone show different things: the big screen carries the question and the show; the phone carries only big answer controls, matched by letter and colour (A/B/C/D).
- Multiple-choice / true-false end early when everyone has answered; matching and closest-number always run the full time and auto-submit what's on the phone.
- Used both among friends and at company parties: humour must stay friendly and office-safe.
- The host's company works in cryptocurrency; a light nod to that world (tickers, tokens, blocks, coins) is welcome, but Qvizz is not a crypto product and must stay readable for guests outside the field.

## Capabilities and Constraints

- ASP.NET Core + SignalR, plain HTML/CSS/JS front end (no framework, no build step), PostgreSQL (Neon) for persistence.
- Phones: mobile-first, portrait, one-hand use, possibly poor connections.
- Presenter: must work on laptop, TV and projector — varied sizes, brightness and contrast (projectors wash out colour and low-contrast detail).
- Interface language: English.
- Fonts and the SignalR client load from CDNs (owner's decision to keep Google Fonts, 2026-11-19): presenter and phones need internet access.
- Answer options can be long: usually a few words, sometimes 5–8 words. Both the big screen and the phone must fit them without truncation.
- Quiz content (texts, images) is supplied later by the host.

## Brand Commitments

- Name: **Qvizz**. Its own brand everywhere; no company logos or company colours.
- Tone: funny, but not too much — entertaining and playable, safe for a company party.
- Visual register: familiar and approachable, "nothing too crazy" (user preference after the first theme round; favourites so far: a 70s TV game-show set and a board-game table).
- Answer coding: each option has a letter and a distinct colour, identical on big screen and phone.

## Evidence on Hand

- Sample quiz in `Quiz/quiz.json`, placeholder shape images in `wwwroot/images/`. No real quiz content, photos or logos yet; do not invent any.

## Product Principles

1. Readable from the back of the room before anything else.
2. The phone is a controller, not a second screen: big targets, minimal text.
3. Fun comes from the moment (countdown, reveal, podium, rank nudges), not from clutter.
4. Friendly for everyone at the party: never mocking, never crude.

## Accessibility & Inclusion

- Answer options are never coded by colour alone (letter always present).
- Timer urgency must not rely only on red.
