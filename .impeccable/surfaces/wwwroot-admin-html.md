---
version: 1
slug: "wwwroot-admin-html"
primary_target: "wwwroot/admin.html"
related_targets: ["wwwroot/game.html"]
---

# Qvizz presenter + player screens

Scope: `/admin` (big screen: laptop, TV, projector) and `/game` (phone). Mode: experience (the game itself leads); phone is a controller.
Audience: friends and colleagues at a crypto-company party, 5–50+ players. Answers up to ~8 words.

## Direction contract

THESIS: The big screen is a family board-game table run like a trading floor: cards, plastic pieces and pawns, with answers laid out as position rows and scores as a ticker. Refuses the flat purple 2x2 colour-block quiz.

OWN-WORLD: Cobalt board ground (#1d3e93), white card stock, printed-track off-white (#f4f1ea), ink navy. Answers are plastic pieces in red / green / blue / yellow with a white letter token and darker bottom edge; True blue, False red. Gold chips (#ffd36b) carry points gained. Bricolage Grotesque for everything, JetBrains Mono for numbers. Player faces drawn on a white face outline, ringed in the player's colour.

STORY: Players see their drawn face join the table, race as pawns on the ticker, watch the room's votes as volume bars, climb the standings, and finish on a podium with medals. Phones show only big answer pieces and a personal result with a nudge about the next rival.

FIRST VIEWPORT: Lobby: "Qvizz." wordmark top-left, white join card with address and three steps, player count in gold numerals, yellow plastic "Start the game" bottom-left; right two-thirds a dark box-insert tray of player compartments (faces up to 30, name tags above). Question: white question card on top with ink timer token right (no ticker during a question, by owner decision), four full-width plastic answer rows below; the ticker returns on the reveal.

FORM: Grounded direction "Game Night" (assigned candidate 4 of the first roll) merged with grounded "Trading Floor" from the safer re-roll at the user's request. seed:74dd9e0877754a78

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

Signature interaction: the reveal: losing rows desaturate, the correct row gets a white ring, volume bars grow to each answer's share, and the first correct faces stack on it. Motion grammar: short ease-out reveals; timer token grows and turns red in the last 5 seconds; ticker crawls when it overflows.

## Surface decisions
- Lobby scales: 4/5/6 columns of faces up to 30 players, names-only tags above 30.
- Closest-number reveal: gold line at the answer, winner's face on it, other guesses as dots, top-5 list.
- Standings: top 5 with rank movement, gain chip, total; "Trade of the round" (biggest climber); played-card pile.
- Design mocks: `.impeccable/mocks/screens/` (board.html).
