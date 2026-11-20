---
name: Qvizz
description: A live party quiz played on a board-game table run like a trading floor.
colors:
  board: "#1d3e93"
  board-deep: "#142d6e"
  board-line: "#3a5bb0"
  track: "#f4f1ea"
  track-line: "#dcd5c4"
  card: "#ffffff"
  ink: "#14213d"
  ink-soft: "#4a5578"
  on-board-soft: "#c9d6ff"
  piece-red: "#d93a28"
  piece-green: "#23843a"
  piece-blue: "#0f78b5"
  piece-yellow: "#f2b705"
  gold: "#ffd36b"
  up: "#23843a"
  down: "#d93a28"
typography:
  display:
    fontFamily: "Bricolage Grotesque, system-ui, sans-serif"
    fontSize: "120px"
    fontWeight: 800
    lineHeight: 0.9
    letterSpacing: "-0.04em"
  headline:
    fontFamily: "Bricolage Grotesque, system-ui, sans-serif"
    fontSize: "60px"
    fontWeight: 800
    lineHeight: 1.06
    letterSpacing: "-0.02em"
  title:
    fontFamily: "Bricolage Grotesque, system-ui, sans-serif"
    fontSize: "40px"
    fontWeight: 600
    lineHeight: 1.1
  body:
    fontFamily: "Bricolage Grotesque, system-ui, sans-serif"
    fontSize: "22px"
    fontWeight: 600
    lineHeight: 1.4
  label:
    fontFamily: "Bricolage Grotesque, system-ui, sans-serif"
    fontSize: "20px"
    fontWeight: 600
  numeral:
    fontFamily: "JetBrains Mono, ui-monospace, monospace"
    fontSize: "48px"
    fontWeight: 800
    lineHeight: 1
    fontFeature: "tnum"
rounded:
  chip: "8px"
  position: "10px"
  field: "14px"
  button: "16px"
  piece: "18px"
  panel: "22px"
  card: "26px"
  tray: "28px"
  tag: "999px"
  token: "50%"
spacing:
  xs: "6px"
  sm: "12px"
  md: "16px"
  lg: "24px"
  xl: "36px"
  stage-edge: "72px"
components:
  button-primary:
    backgroundColor: "{colors.piece-yellow}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.button}"
    padding: "16px 28px 22px"
  button-light:
    backgroundColor: "{colors.card}"
    textColor: "{colors.ink}"
    rounded: "{rounded.button}"
    padding: "16px 28px 22px"
  answer-row-a:
    backgroundColor: "{colors.piece-red}"
    textColor: "{colors.card}"
    typography: "{typography.title}"
    rounded: "{rounded.piece}"
    padding: "12px 34px 18px 18px"
  answer-row-b:
    backgroundColor: "{colors.piece-green}"
    textColor: "{colors.card}"
    rounded: "{rounded.piece}"
  answer-row-c:
    backgroundColor: "{colors.piece-blue}"
    textColor: "{colors.card}"
    rounded: "{rounded.piece}"
  answer-row-d:
    backgroundColor: "{colors.piece-yellow}"
    textColor: "{colors.ink}"
    rounded: "{rounded.piece}"
  letter-token:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.token}"
    size: "74px"
  timer-token:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.card}"
    typography: "{typography.numeral}"
    rounded: "{rounded.token}"
    size: "136px"
  points-chip:
    backgroundColor: "{colors.gold}"
    textColor: "{colors.ink}"
    rounded: "{rounded.chip}"
    padding: "2px 10px"
  question-card:
    backgroundColor: "{colors.card}"
    textColor: "{colors.ink}"
    rounded: "{rounded.card}"
    padding: "36px 250px 30px 52px"
  ticker-position:
    backgroundColor: "{colors.card}"
    textColor: "{colors.ink}"
    rounded: "{rounded.position}"
    padding: "5px 14px 5px 5px"
  input-field:
    backgroundColor: "{colors.track}"
    textColor: "{colors.ink}"
    rounded: "{rounded.field}"
    padding: "14px 16px"
  player-tag:
    backgroundColor: "{colors.track}"
    textColor: "{colors.ink}"
    rounded: "{rounded.tag}"
    padding: "8px 18px 8px 10px"
---

# Design System: Qvizz

## Overview

**Creative North Star: "Game Night on the Trading Floor"**

The big screen is a family board-game table laid on a cobalt board: white card stock carries the words, glossy plastic pieces carry the answers, drawn faces sit in a box-insert tray like pawns, and the scores run along the top like a market ticker. The trading-floor half of the world is a light nod, never a costume: answers are "positions" with volume bars, standings show rank movement arrows and a "trade of the round", numbers are set in a monospace tape face. Nothing reads as a crypto product.

Density is low and sized for the back of the room. The presenter view is a fixed 1440 by 900 stage scaled to fit any laptop, TV or projector, so every measurement on it is designed once at full size. The phone is a controller: big stacked pieces, one card of text, almost nothing else. Fun is carried by moments (dealt-card countdown, reveal, podium rise), not by decoration.

The world rejects the flat purple 2x2 colour-block quiz look: answers are full-width plastic rows with a darker bottom edge, never four flat tiles.

**Key Characteristics:**
- Cobalt board ground; white card stock and off-white printed track sit on it.
- Answer pieces in four saturated plastic colours, each always paired with its letter token.
- Plastic pieces have a darker inset bottom edge; cards lift softly with no edge.
- One display family at heavy weight; every number in a tabular monospace.
- Gold means points and attention.
- Player faces are hand-drawn on a white face, ringed in the player's own colour.

## Colors

A deep cobalt table with paper-white cards and four primary-toy plastic colours, punctuated by a warm gold for points.

### Primary
- **Cobalt Board** (#1d3e93): the page ground on both screens; the table everything sits on. Also the ribbon colour of the medal.
- **Deep Board** (#142d6e): recessed surfaces cut into the board: the lobby tray, the closest-number chart, corner controls.
- **Board Groove** (#3a5bb0): rules drawn on the board, such as the number-line axis.

### Secondary
- **Piece Red** (#d93a28): answer A, False, the timer token's last 5 seconds, rank-down and error text.
- **Piece Green** (#23843a): answer B, correct badge, matched-pair number, rank-up.
- **Piece Blue** (#0f78b5): answer C, True.
- **Piece Yellow** (#f2b705): answer D (with ink text), the primary button, the wordmark's full stop, the first-place podium block.

### Tertiary
- **Points Gold** (#ffd36b): points-gained chips, the gold player count, the closest-number target line and winner row, the standings subtitle numerals, the new-player ring, the focus ring and text selection.

### Neutral
- **Card Stock** (#ffffff): question card, join card, standings sheet, phone panels and result card, letter tokens, the winning-row ring.
- **Printed Track** (#f4f1ea): the ticker strip, lobby seats, name tags, input wells, "trade of the round", the phone "me" bar.
- **Track Rule** (#dcd5c4): hairlines and input borders on track surfaces, the standings header rule.
- **Ink Navy** (#14213d): all text on light surfaces; the timer token; matching numbers.
- **Soft Ink** (#4a5578): secondary text on light surfaces: meta lines, column heads, units.
- **Board Mist** (#c9d6ff): secondary text on the board.

### Player colours
Ten pawn colours assigned by join order and cycled: #d93a28, #23843a, #0f78b5, #f2b705, #864cbf, #0aa3a3, #e86fa6, #7a4a1d, #3a3a3a, #ff8c1a. A player keeps one colour everywhere (face ring, name-tag dot). The first four deliberately reuse the answer pieces.

### Named Rules
**The Letter Always Rides Along Rule.** An answer colour never appears without its letter token (A/B/C/D, or the True/False word). Colour is a second channel, not the code.

**The Fixed Mapping Rule.** A red, B green, C blue, D yellow; True blue, False red. Identical on big screen and phone, in every question.

**The Gold Means Points Rule.** Gold is for points earned and for pointing at someone (winner, newcomer, focus). It is not a decorative accent.

## Typography

**Display Font:** Bricolage Grotesque (with system-ui, sans-serif)
**Body Font:** Bricolage Grotesque (same family)
**Label/Mono Font:** JetBrains Mono (with ui-monospace, monospace), numbers only

**Character:** a chunky, friendly grotesque at 800 for anything read from across the room, 600 for supporting text; a monospace at 800 makes every score, rank, timer and count read like ticker tape. Fonts load from Google Fonts (weights 500/600/800 and 600/800) by the owner's decision; the presenter and phones need internet access, and without it the system font is the fallback.

### Hierarchy (big-screen stage px; the stage scales as a whole)
- **Display** (800, 84 to 120px, 0.9, -0.03 to -0.04em): wordmark, countdown title, podium title.
- **Headline** (800, 60px default via a per-question size, 1.06, -0.02em, balanced wrap): question text; standings title at 64px; chart heading at 52px.
- **Title** (600, 40px, 1.1): answer text in rows; player names in standings and podium at 800.
- **Body** (600, 22 to 28px): meta lines, join steps, sub-captions. Phone body 17 to 21px.
- **Label** (600, 20 to 22px, Soft Ink): column heads, units, secondary captions.
- **Numeral** (JetBrains Mono 800, tabular): 18px ticker ranks up to 300px on the countdown card; totals and ranks at 48px.

### Named Rules
**The Numbers Are Tape Rule.** Every digit that counts something (score, rank, timer, vote count, player count, answer value) is set in the monospace at 800 with tabular figures. Words never are.

**The Two Weights Rule.** Text is 800 or 600. Nothing light, nothing italic.

## Layout

The presenter view is a fixed 1440 by 900 stage, centred and scaled as a whole to fit the window, so layout is authored in absolute pixels once. The stage keeps a 72px side margin (22px top, 44px bottom) and a 24px gap between bands. Question screens stack two bands, with no ticker so players can focus: the white question card with the timer token at its right, then the answer area filling the rest (reveal screens add the ticker strip on top, bled 48px wider than the margin): four full-width rows with 16px gaps, two tall side-by-side pieces for True/False, a 1 : 1.6 two-column grid for matching, a deep-board chart for closest number.

Feature screens (lobby, standings, podium) position blocks absolutely on the stage: lobby has wordmark, join card, gold count and Start on the left 470px column, and the tray filling the right from x 600. The tray scales by player count: 4 columns of 104px faces up to 12 players, 5 columns of 88px up to 20, 6 columns of 70px up to 30, with dashed "free seat" slots filling three rows; above 30 it switches to wrapping name tags with a colour dot and no faces. Standings is a 930px white sheet with a six-column grid (rank, move, face, name, gain, total) and a side column for the trade card and the played-card pile.

The phone is a single column, max 520px, 14px padding and 12px gaps, filling the dynamic viewport height: question card on top, answer pieces stretching to fill, the "me" bar at the bottom.

## Elevation & Depth

Depth is physical and comes in two kinds. Plastic pieces (answer rows, buttons, tokens, badges, podium blocks) have a darker inset bottom edge, as if moulded, plus a short drop shadow; they press down 3px when tapped. Paper (cards, panels, sheets, the dealt cards) has no edge and lifts with a soft, larger shadow. Recessed areas (tray, chart) are flat and darker instead of shadowed.

### Shadow Vocabulary
- **Plastic piece** (`box-shadow: inset 0 -10px 0 rgba(0,0,0,.18), 0 8px 14px rgba(0,0,0,.3)`): answer rows and buttons; podium blocks use a 14px edge.
- **Token edge** (`box-shadow: inset 0 -10px 0 rgba(0,0,0,.35)`): the timer token (5px on phone); badges use 8px at .2.
- **Card lift** (`box-shadow: 0 18px 34px rgba(0,0,0,.32)`): big-screen cards: question, join, standings sheet, login.
- **Panel lift** (`box-shadow: 0 10px 18px rgba(0,0,0,.3)`): phone panels, result card, drawing pad, pile cards.
- **Item lift** (`box-shadow: 0 8px 14px rgba(0,0,0,.28)`): matching items and the phone question card.
- **Movable cell** (`box-shadow: 0 4px 0 rgba(0,0,0,.2)`): the right-hand matching cells a player can pick up.

### Named Rules
**The Plastic Or Paper Rule.** A surface is either a piece (coloured, inset bottom edge, presses) or paper (white or track, soft lift, no edge). Never give a card a moulded edge, never give a piece a paper shadow alone.

## Shapes

Generously rounded, toy-like corners scaled to object size: 8px chips, 10px ticker positions, 14px fields and seats, 16px buttons, 18px answer pieces, 22px phone panels, 26px big cards, 28px tray, fully round tokens, faces and name tags. Plastic pieces carry asymmetric padding (more at the bottom) so the label sits above the moulded edge. Faces are circles with a solid ring in the player colour (dashed while the player is still drawing). Podium blocks round the top corners (22px) and barely round the base (8px).

## Components

### Buttons
Chunky plastic, ink on yellow.
- **Shape:** gently rounded (16px), moulded bottom edge.
- **Primary:** Piece Yellow with Ink text, 800 weight, 22px, padding 16px 28px 22px; larger on feature screens (Start the game 32px).
- **Light:** Card Stock background, same shape.
- **Press / Focus:** pressing sinks 3px (0.12s); focus is a 4px Points Gold outline offset 3px; disabled at 55% opacity.
- **Corner controls:** small Deep Board pills (14px, 600) at 70% opacity in the stage corner for full screen and reset, full opacity on hover or focus.

### Chips
- **Points chip:** monospace 800, Ink on Points Gold, 8px radius, tight padding; a zero gain turns muted (#eee9de with Soft Ink).
- **Rank move:** monospace with an SVG up/down triangle or flat bar, green up, red down, grey same; on phone it is a filled pill.

### Cards / Containers
- **Corner Style:** 26px on the big screen, 22 to 24px on phone.
- **Background:** Card Stock; secondary cards in Printed Track.
- **Shadow Strategy:** Card lift or Panel lift (see Elevation).
- **Border:** none; the standings sheet separates rows with 2px track rules and highlights first place with a pale gold wash.
- **Internal Padding:** 22 to 36px on the stage, 18 to 24px on phone.

### Inputs / Fields
- **Style:** Printed Track well, 3px Track Rule border, 14px radius, 600 weight at 22 to 24px, red caret.
- **Focus:** border turns Ink.
- **Number field:** 3px Ink border, monospace 56px digits with the unit in Soft Ink.
- **Error:** red 600 text below, collapsed when empty.

### Answer Pieces (signature)
Full-width plastic rows in the fixed answer colour with a white round letter token (74px on stage, 52px on phone) on the left, text at 40px/600 on stage. On reveal, wrong rows desaturate and dim (saturate .35, brightness .75), the correct row gets a 6px white ring, a white volume bar grows along the bottom edge to each answer's share (1.2s ease-out), and the first correct faces stack on it beside the monospace vote count. On phone the selected piece gets a 5px white ring and the others dim.

### Timer Token
A 136px Ink disc with monospace seconds at the right of the question card. In the last 5 seconds it turns red, scales up and pulses, so urgency is carried by size and motion as well as colour.

### Ticker
A Printed Track strip with every player as a white position (rank, face, name, total, gain chip). It crawls linearly and loops when it overflows. It appears on reveal screens only: while a question is running the screen shows no scores, so players can focus on the question.

### Player Faces and Lobby Tray
Faces are drawn on the phone in a white square with a dark face outline to draw into; the result is a white face on white, ringed in the player's colour. Until drawn, a seat shows the initial in a dashed ring. New arrivals seat with a 0.5s scale-in and a gold ring; offline players fade to 35%.

### Podium
Three plastic blocks (gold, silver #c9ced8, bronze #d08a4e) rising in order 3, 2, 1 with SVG medals on cobalt ribbons and the winner's face larger.

## Do's and Don'ts

### Do:
- **Do** pair every answer colour with its letter token: A red, B green, C blue, D yellow; True blue, False red.
- **Do** set every counted number in JetBrains Mono 800 with tabular figures.
- **Do** build new presenter screens on the fixed 1440 by 900 stage with the 72px side margin, sized for the back of the room.
- **Do** give coloured pieces the moulded inset bottom edge and a 3px press; give white cards a soft lift only.
- **Do** use Points Gold for points and for pointing at a person; keep focus rings as a 4px gold outline.
- **Do** keep motion short and ease-out (cubic-bezier(.16, 1, .3, 1)) and switch it all off under reduced motion.
- **Do** draw icons as inline SVG (check, cross, rank triangles, medal).

### Don't:
- **Don't** lay four answers out as flat, edge-less 2x2 colour tiles.
- **Don't** use text weights below 600 or italics.
- **Don't** change or extend the answer colour mapping per screen; a fifth or sixth matching row uses the pair colours (#864cbf, #0aa3a3), never a reshuffle.
- **Don't** set words in the monospace or numbers in the display face.
- **Don't** use company logos or company colours; Qvizz is its own brand.
