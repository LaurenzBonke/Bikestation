# Builds docs/journey/Smart-Bikestation-User-Journey.pptx with PowerPoint (COM).
# Colours match the dashboard (Frontend/api/src/style.css). Run:
#   powershell -ExecutionPolicy Bypass -File docs\journey\build-journey-pptx.ps1

$ErrorActionPreference = "Stop"
$out = Join-Path $PSScriptRoot "Smart-Bikestation-User-Journey.pptx"

function Rgb([string]$hex) {
    $r = [Convert]::ToInt32($hex.Substring(1, 2), 16); $g = [Convert]::ToInt32($hex.Substring(3, 2), 16); $b = [Convert]::ToInt32($hex.Substring(5, 2), 16)
    return $r + 256 * $g + 65536 * $b   # PowerPoint wants BGR
}
$C = @{
    bg = Rgb "#f4f5f1"; surface = Rgb "#ffffff"; surface2 = Rgb "#f3f6f2"; border = Rgb "#e5e9e3"
    ink = Rgb "#202a27"; text = Rgb "#3f4943"; muted = Rgb "#5d6660"
    green = Rgb "#1d7656"; greenDark = Rgb "#155f44"; greenSoft = Rgb "#e2f1e8"
    red = Rgb "#b3473b"; redSoft = Rgb "#fdecea"; accent = Rgb "#e36e4e"; white = Rgb "#ffffff"
}
$Head = "Segoe UI Semibold"; $Body = "Segoe UI"

$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Add($false)
$pres.PageSetup.SlideWidth = 960; $pres.PageSetup.SlideHeight = 540
$script:n = 0

function New-Slide([int]$bg = $C.bg) {
    $script:n++
    $s = $pres.Slides.Add($script:n, 12)   # ppLayoutBlank
    $s.FollowMasterBackground = 0
    $s.Background.Fill.Solid(); $s.Background.Fill.ForeColor.RGB = $bg
    return $s
}

# radius: <= 1 = fraction of the short side, otherwise points
function Box($s, $x, $y, $w, $h, $fill, [int]$line = -1, [int]$shape = 5, [double]$radius = 10) {
    $b = $s.Shapes.AddShape($shape, $x, $y, $w, $h)
    $b.Fill.ForeColor.RGB = $fill
    if ($line -ge 0) { $b.Line.ForeColor.RGB = $line; $b.Line.Weight = 1 } else { $b.Line.Visible = 0 }
    if ($shape -eq 5) { $b.Adjustments.Item(1) = [math]::Min(0.5, $(if ($radius -le 1) { $radius } else { $radius / [math]::Min($w, $h) })) }
    $b.Shadow.Visible = 0
    return $b
}

function Text($s, $x, $y, $w, $h, [string]$t, [double]$size = 14, [int]$color = $C.text, [string]$font = $Body, [int]$align = 1, [bool]$bold = $false) {
    $tb = $s.Shapes.AddTextbox(1, $x, $y, $w, $h)
    $tf = $tb.TextFrame; $tf.WordWrap = -1; $tf.AutoSize = 0
    $tf.MarginLeft = 0; $tf.MarginRight = 0; $tf.MarginTop = 0; $tf.MarginBottom = 0
    $r = $tf.TextRange; $r.Text = $t
    $r.Font.Name = $font; $r.Font.Size = $size; $r.Font.Color.RGB = $color; $r.Font.Bold = [int]$bold
    $r.ParagraphFormat.Alignment = $align
    $r.ParagraphFormat.SpaceWithin = 1.1
    return $tb
}

function Kicker($s, $x, $y, [string]$t, [int]$color = $C.green) {
    $l = $s.Shapes.AddLine($x, $y + 7, $x + 24, $y + 7); $l.Line.ForeColor.RGB = $C.accent; $l.Line.Weight = 2
    Text $s ($x + 32) $y 600 16 $t 10.5 $color $Head 1 $true | Out-Null
}

function Title($s, [string]$kicker, [string]$title, [string]$sub = "") {
    Kicker $s 48 34 $kicker
    Text $s 48 56 860 44 $title 30 $C.ink $Head | Out-Null
    if ($sub) { Text $s 48 102 860 40 $sub 13.5 $C.text | Out-Null }
}

function Pill($s, $x, $y, [string]$t, $fill, $color, [double]$w = 0) {
    if ($w -eq 0) { $w = 12 + 6.4 * $t.Length }
    $b = Box $s $x $y $w 20 $fill -1 5 0.5
    $r = $b.TextFrame.TextRange; $r.Text = $t; $r.Font.Size = 9; $r.Font.Name = $Head; $r.Font.Bold = 1; $r.Font.Color.RGB = $color
    $b.TextFrame.MarginLeft = 2; $b.TextFrame.MarginRight = 2; $b.TextFrame.MarginTop = 0; $b.TextFrame.MarginBottom = 0
    return $b
}

function Footer($s, [string]$t = "Smart Bikestation · User journey") {
    Text $s 48 510 500 14 $t 9 $C.muted | Out-Null
    Text $s 760 510 152 14 ("{0}" -f $script:n) 9 $C.muted $Body 3 | Out-Null
}

function Logo($s, $x, $y, [double]$size = 40) {
    $b = Box $s $x $y $size $size $C.green -1 5 0.22
    $r = $b.TextFrame.TextRange; $r.Text = [string][char]0xD83D + [char]0xDEB2; $r.Font.Size = $size * 0.5; $r.Font.Color.RGB = $C.white
}

# ------------------------------------------------------------------ 1 Title
$s = New-Slide $C.ink
Logo $s 60 60 48
Text $s 120 68 400 34 "bikestation." 26 $C.white $Head | Out-Null
Text $s 60 170 820 120 "The user journey`nof the Smart Bikestation" 44 $C.white $Head | Out-Null
Text $s 60 300 760 60 "From finding a free spot to picking up the bike – and what happens if someone tries to steal it." 17 (Rgb "#c9d3cd") | Out-Null
Pill $s 60 390 "HACKATHON 2026" $C.green $C.white 120 | Out-Null
Pill $s 190 390 "WORKING PROTOTYPE" (Rgb "#2e3a35") (Rgb "#dfe6e1") 150 | Out-Null
Text $s 60 480 840 20 "Live demo: Stellplatz 1 · Hackathon Halle" 12 (Rgb "#9fb0a7") | Out-Null

# ------------------------------------------------------------------ 2 Problem & personas
$s = New-Slide
Title $s "CONTEXT" "Who is this for?" "Three people interact with the station. The journey is designed around what each of them needs in the moment."
$cards = @(
    @("Cyclist", "Student or commuter with a valuable bike or e-bike.", "Needs: know if a spot is free before arriving · lock the bike without a key · be told immediately if something happens."),
    @("Admin / operator", "Runs the station (school, city, company).", "Needs: see who is at which box · react to alarms · add or remove stations · a clear log of events."),
    @("Station", "Raspberry Pi 5 with ultrasonic sensor, servo latch and LED.", "Needs: a simple rule – measure, report, move the latch as the server says.")
)
for ($i = 0; $i -lt 3; $i++) {
    $x = 48 + $i * 292
    Box $s $x 160 276 220 $C.surface $C.border | Out-Null
    $bar = Box $s $x 160 5 220 @($C.green, $C.accent, $C.greenDark)[$i] -1 1
    Text $s ($x + 22) 180 240 26 $cards[$i][0] 20 $C.ink $Head | Out-Null
    Text $s ($x + 22) 214 240 50 $cards[$i][1] 12.5 $C.text | Out-Null
    Text $s ($x + 22) 280 240 160 $cards[$i][2] 12 $C.muted | Out-Null
}
Footer $s

# ------------------------------------------------------------------ 3 Journey overview
$s = New-Slide
Title $s "THE JOURNEY" "Five steps from free spot to pick-up" "Every step is one click for the cyclist. The server decides, the station executes."
$steps = @(
    @("1", "See a free spot", "Overview shows ""Stellplatz 1 is free"" with its location.", "", 0),
    @("2", "Book", "One click – the servo latch opens the box.", "LATCH OPEN", 1),
    @("3", "Park", "Ultrasonic detects the bike; after 5 s the box locks.", "LATCH CLOSED", 2),
    @("4", "Pick up", "Tap ""Pick up"" – the box opens again.", "LATCH OPEN", 1),
    @("5", "Done", "Bike removed for 5 s – box closes and is free again.", "LATCH CLOSED", 2)
)
$line = $s.Shapes.AddLine(120, 207, 840, 207); $line.Line.ForeColor.RGB = $C.green; $line.Line.Weight = 2; $line.Line.DashStyle = 4
for ($i = 0; $i -lt 5; $i++) {
    $cx = 120 + $i * 180
    $circle = Box $s ($cx - 24) 183 48 48 $C.green -1 9
    $r = $circle.TextFrame.TextRange; $r.Text = $steps[$i][0]; $r.Font.Size = 18; $r.Font.Name = $Head; $r.Font.Color.RGB = $C.white
    Text $s ($cx - 80) 246 160 24 $steps[$i][1] 16 $C.ink $Head 2 | Out-Null
    Text $s ($cx - 80) 274 160 70 $steps[$i][2] 11.5 $C.text $Body 2 | Out-Null
    if ($steps[$i][4] -eq 1) { Pill $s ($cx - 45) 350 $steps[$i][3] $C.greenSoft $C.greenDark 90 | Out-Null }
    if ($steps[$i][4] -eq 2) { $closed = Pill $s ($cx - 50) 350 $steps[$i][3] $C.surface $C.ink 100; $closed.Line.Visible = -1; $closed.Line.ForeColor.RGB = $C.border }
}
$a = Box $s 48 410 864 64 $C.redSoft (Rgb "#efc4bd")
$bar = Box $s 48 410 6 64 $C.red -1 1
Text $s 72 420 820 50 ("And if the bike leaves the locked box without being opened?  The owner gets an alarm with sound, the box is blocked and the admin sees exactly who, when and where.") 13 $C.red $Body | Out-Null
Footer $s

# ------------------------------------------------------------------ helper: step detail slide
function Step-Slide([string]$kicker, [string]$title, [string]$sub, [string[]]$user, [string[]]$system, [string]$mockTitle, [string]$mockSub, [string]$mockBtn, [bool]$mockDark = $true) {
    $s = New-Slide
    Title $s $kicker $title $sub
    # left: what the user does / sees
    Box $s 48 160 420 300 $C.surface $C.border | Out-Null
    Text $s 70 176 380 18 "WHAT THE CYCLIST DOES" 10 $C.muted $Head 1 $true | Out-Null
    $y = 202
    foreach ($u in $user) {
        $d = Box $s 70 ($y + 5) 8 8 $C.green -1 9
        Text $s 88 $y 360 40 $u 13 $C.ink | Out-Null
        $y += 44
    }
    # mock phone card
    $m = Box $s 70 382 376 62 $C.surface (@($C.border, $C.ink)[[int]$mockDark])
    $m.Line.Weight = @(1, 2)[[int]$mockDark]
    Text $s 88 392 230 22 $mockTitle 15 $C.ink $Head | Out-Null
    Text $s 88 416 230 18 $mockSub 10.5 $C.text | Out-Null
    $btn = Box $s 326 398 108 30 $C.ink -1 5 0.5
    $r = $btn.TextFrame.TextRange; $r.Text = $mockBtn; $r.Font.Size = 10.5; $r.Font.Name = $Head; $r.Font.Color.RGB = $C.white
    # right: behind the scenes
    Box $s 490 160 422 300 $C.ink -1 | Out-Null
    Text $s 512 176 380 18 "BEHIND THE SCENES" 10 (Rgb "#9fb0a7") $Head 1 $true | Out-Null
    $y = 202
    foreach ($t in $system) {
        Text $s 512 $y 20 20 ([string][char]0x2192) 13 (Rgb "#5aa37e") $Head | Out-Null
        Text $s 534 $y 356 44 $t 12.5 (Rgb "#dfe6e1") | Out-Null
        $y += 50
    }
    Footer $s
}

# ------------------------------------------------------------------ 4 Find & book
Step-Slide "STEP 1 + 2" "Find a free spot and book it" "No app store, no key: the web app works on any phone or laptop." `
    @("Opens the site and sees ""Stellplatz 1 is free"" with location.", "Taps the spot – lands on the booking page with the box highlighted.", "Signs in (username + password only) and taps ""Book"".", "The box opens in front of them.") `
    @("Overview refreshes every 3 s from GET /api/slots.", "POST /api/boxes/1/book (JWT) – only if the station is online and the user has no other box.", "Box state: Free → OpenForParking, a BoxEvent ""Booked"" is logged.", "The Pi's next reading gets lockOpen: true – servo turns to 90°.") `
    "Stellplatz 1 is free" "Hackathon Halle" "Book now"

# ------------------------------------------------------------------ 5 Park & lock
Step-Slide "STEP 3" "Park – the box locks itself" "Detection is debounced so a hand or a single bad reading never locks the box." `
    @("Pushes the bike into the box.", "Waits a moment – nothing else to do.", "Sees ""Your bike is safely locked"" on their phone.", "Green LED on the station goes off.") `
    @("Pi measures distance every second (HC-SR04) and POSTs it.", "Bike ≤ 7 cm for 5 s → OpenForParking → Locked.", "Answer lockOpen: false – servo moves in 10 soft steps to 45°.", "If no bike arrives within 120 s the booking is released.") `
    "Locked" "Your bike is safe" "Pick up" $false

# ------------------------------------------------------------------ 6 Pick up
Step-Slide "STEP 4 + 5" "Pick up and leave" "The box only opens for its owner – and closes again by itself." `
    @("Taps ""Pick up"" in the app.", "The box opens, the bike comes out.", "Walks away – the box closes and is free for the next person.", "History shows the completed parking.") `
    @("POST /api/boxes/1/pickup – only the owner, only when Locked.", "Locked → OpenForPickup, lockOpen: true.", "Bike gone for 5 s → Free, parking ended ""Completed"".", "Not taken within 120 s → locks again (bike stays safe).") `
    "Stellplatz 1" "Open for pickup" "Opening..." $false

# ------------------------------------------------------------------ 7 Alarm journey
$s = New-Slide
Title $s "WHEN SOMETHING GOES WRONG" "The alarm journey" "The bike disappears from a locked box without the owner opening it."
$flow = @(
    @("Bike removed", "Station reports ""nothing there"" while the box is Locked."),
    @("3 s confirmed", "Server: Locked → Blocked, parking ended ""BikeRemoved"", critical alert."),
    @("Owner alerted", "Red banner on every page, repeating chime, vibration, blinking tab."),
    @("Admin reacts", "Sees the alarm, the user and the time; checks the station on site."),
    @("Released", "Admin releases the box – logged with the admin's name.")
)
for ($i = 0; $i -lt 5; $i++) {
    $x = 48 + $i * 176
    $fill = @($C.surface, $C.redSoft, $C.redSoft, $C.surface, $C.greenSoft)[$i]
    Box $s $x 160 160 170 $fill $C.border | Out-Null
    Text $s ($x + 16) 176 130 20 ("{0}" -f ($i + 1)) 22 @($C.ink, $C.red, $C.red, $C.ink, $C.green)[$i] $Head | Out-Null
    Text $s ($x + 16) 208 130 24 $flow[$i][0] 15 $C.ink $Head | Out-Null
    Text $s ($x + 16) 236 130 90 $flow[$i][1] 11.5 $C.text | Out-Null
    if ($i -lt 4) { Text $s ($x + 161) 232 16 20 ([string][char]0x203A) 20 $C.muted $Head | Out-Null }
}
$b = Box $s 48 352 864 110 $C.redSoft (Rgb "#efc4bd")
$bar = Box $s 48 352 6 110 $C.red -1 1
Text $s 72 368 600 24 "Your bike was unexpectedly removed from Stellplatz 1!" 17 $C.red $Head | Out-Null
Text $s 72 396 600 40 "The box was not opened. The operator has been informed and is checking the station." 12.5 $C.text | Out-Null
$btn = Box $s 700 384 190 40 $C.surface $C.ink
$r = $btn.TextFrame.TextRange; $r.Text = "Got it – stop alarm"; $r.Font.Size = 12; $r.Font.Name = $Head; $r.Font.Color.RGB = $C.ink
Text $s 72 438 820 20 "Acknowledging the message stops sound, vibration and blinking. The box stays blocked until an admin releases it." 10.5 $C.muted | Out-Null
Footer $s

# ------------------------------------------------------------------ 8 Admin journey
$s = New-Slide
Title $s "THE ADMIN JOURNEY" "Keeping an eye on every station" "One page, ordered by urgency: who is where, what needs action, and the full history at the bottom."
$admin = @(
    @("Who is at which box?", "Stellplatz 1 · Hackathon Halle`nOccupied by testfahrer · bike inside since 10:41 · Locked"),
    @("Manage stations", "Add a station (gets the next number = hardware slot_id), change its location, delete a free station."),
    @("Open alerts & blocked boxes", "Tampering, AI anomalies, bike removed → resolve; blocked boxes → release after checking on site."),
    @("Box log (dropdown)", """testfahrer booked Stellplatz 1 (Hackathon Halle) on 29.09.2026 at 10:41:06 – latch opened.""  Filter by user.")
)
for ($i = 0; $i -lt 4; $i++) {
    $x = 48 + ($i % 2) * 438; $y = 160 + [math]::Floor($i / 2) * 150
    Box $s $x $y 426 136 $C.surface $C.border | Out-Null
    Pill $s ($x + 20) ($y + 18) ("{0}" -f ($i + 1)) $C.green $C.white 24 | Out-Null
    Text $s ($x + 54) ($y + 17) 350 24 $admin[$i][0] 16 $C.ink $Head | Out-Null
    Text $s ($x + 20) ($y + 52) 390 80 $admin[$i][1] 12 $C.text | Out-Null
}
Footer $s

# ------------------------------------------------------------------ 9 Design principles
$s = New-Slide
Title $s "DESIGN PRINCIPLES" "Built in from the start" ""
$p = @(
    @("Privacy by design", "No cameras. Accounts are username + password hash only. Sensor readings are deleted after 90 days. Other users' names are visible to admins only."),
    @("Accessible", "Clear text instead of colour codes (""Stellplatz 1 is free""), keyboard and screen-reader friendly, light/dark mode, German, English and Dutch."),
    @("Safe by default", "The server decides, the station executes. Only the owner opens a box. A blocked box stays closed until a human checks it."),
    @("Tested", "61 automated tests cover booking, latch, alarm, permissions, timeouts, stations and the admin log.")
)
for ($i = 0; $i -lt 4; $i++) {
    $x = 48 + ($i % 2) * 438; $y = 130 + [math]::Floor($i / 2) * 170
    Box $s $x $y 426 154 $C.surface $C.border | Out-Null
    Box $s $x $y 426 5 @($C.green, $C.accent, $C.greenDark, $C.ink)[$i] -1 1 | Out-Null
    Text $s ($x + 22) ($y + 22) 380 26 $p[$i][0] 18 $C.ink $Head | Out-Null
    Text $s ($x + 22) ($y + 58) 382 90 $p[$i][1] 12.5 $C.text | Out-Null
}
Footer $s

# ------------------------------------------------------------------ 10 Architecture
$s = New-Slide
Title $s "UNDER THE HOOD" "How the journey is powered" "One rule keeps it simple: the station measures, the server decides, the app shows."
$nodes = @(
    @("Station", "Raspberry Pi 5`nHC-SR04 ultrasonic`nServo latch · green LED`nparking_sensor.py"),
    @("Server", "ASP.NET Core 10 (C#)`nBox state machine`nSQLite · background jobs`nport 8080"),
    @("Web app", "React + TypeScript`nUser & admin pages`nrefresh every 2–3 s`nDE · EN · NL")
)
for ($i = 0; $i -lt 3; $i++) {
    $x = 48 + $i * 300
    Box $s $x 160 264 170 @($C.surface, $C.ink, $C.surface)[$i] $C.border | Out-Null
    Text $s ($x + 22) 178 220 26 $nodes[$i][0] 19 @($C.ink, $C.white, $C.ink)[$i] $Head | Out-Null
    Text $s ($x + 22) 214 220 110 $nodes[$i][1] 12.5 @($C.text, (Rgb "#dfe6e1"), $C.text)[$i] | Out-Null
    if ($i -lt 2) { Text $s ($x + 268) 226 30 30 ([string][char]0x2194) 22 $C.green $Head 2 | Out-Null }
}
Text $s 48 350 420 20 "Station ↔ Server" 12 $C.ink $Head | Out-Null
Text $s 48 370 420 60 "Every second: POST /api/sensor-data {slotId, distance} with API key → answer {boxState, lockOpen}." 11.5 $C.text | Out-Null
Text $s 492 350 420 20 "Server ↔ Web app" 12 $C.ink $Head | Out-Null
Text $s 492 370 420 60 "REST + JWT: book, cancel, pick up; admin: occupancy, stations, alerts, log." 11.5 $C.text | Out-Null
Pill $s 48 440 "States: Free → OpenForParking → Locked → OpenForPickup → Free   ·   Locked → Blocked (alarm) → Free" $C.greenSoft $C.greenDark 864 | Out-Null
Footer $s

# ------------------------------------------------------------------ 11 Next steps
$s = New-Slide $C.ink
Kicker $s 48 34 "WHAT'S NEXT" (Rgb "#9fb0a7")
Text $s 48 56 860 44 "Improving the journey further" 30 $C.white $Head | Out-Null
$next = @(
    @("Alarm on the lock screen", "HTTPS + Web Push, so the alarm reaches the phone even when the browser is closed."),
    @("More stations", "Each new box just needs the same hardware and its number – the admin adds it in one click."),
    @("Second sensor", "Weight sensor via ADS1115 as a second opinion next to ultrasonic."),
    @("Own server", "Move from the Windows PC to Linux (Pi 5 / Proxmox) for 24/7 operation.")
)
for ($i = 0; $i -lt 4; $i++) {
    $y = 130 + $i * 84
    $circle = Box $s 48 $y 40 40 $C.green -1 9
    $r = $circle.TextFrame.TextRange; $r.Text = ("{0}" -f ($i + 1)); $r.Font.Size = 16; $r.Font.Name = $Head; $r.Font.Color.RGB = $C.white
    Text $s 108 ($y + 1) 780 24 $next[$i][0] 17 $C.white $Head | Out-Null
    Text $s 108 ($y + 28) 780 40 $next[$i][1] 12.5 (Rgb "#c9d3cd") | Out-Null
}
Text $s 48 490 860 20 "Smart Bikestation · Hackathon 2026 · Try it live at Stellplatz 1, Hackathon Halle" 11 (Rgb "#9fb0a7") | Out-Null

$pres.SaveAs($out)
$pres.Close()
$pp.Quit()
Write-Host "Saved $out"
