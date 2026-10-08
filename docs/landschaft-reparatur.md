# Reparatur, Grafik, Gehen und Leistung in der Landschaft (2026-10-07/08)

Auftrag: der „Reparaturprompt“ des Betreibers, danach „die Grafik muss auf der App verbessert werden“, danach „weiter machen“ (Phase 8 der Regeln: Gehen und Komfort, Abschnitt 4), danach noch einmal „weiter machen“ (Phase 9: LOD, Instancing, Leistung, Abschnitt 5). Die Landschaft war ein dichter, zufällig gefüllter Low-Poly-Wald; sie soll eine bewusst komponierte, ruhige, offene Naturwelt für die Quest 3S sein, die dem Konzeptbild (`konzept-landschaft.png`) in Stimmung, Staffelung und Farbe folgt. **Reparatur durch Reduktion und Neuplatzierung, danach Licht, Farbe und Detail, nicht durch mehr Objekte.** Was bleibt: Projekt, URP-/OpenXR-Setup, Menübefehle, Szene, Ablauf „Generate → Validate“.

**Nichts davon wurde in Unity oder an einer Quest geprüft** (siehe Abschnitt 8). Geprüft wurde mit einem Werkzeug außerhalb von Unity: derselbe C#-Code baut die Szene und bäckt das Licht, Checks messen die Regeln, eine Vorschau zeigt genau die gebackenen Vertexfarben aus Augenhöhe.

## 1. Analyse der alten Szene (aus dem Code gerechnet, nicht gemessen)

| Teil | Befund | Folge |
|---|---|---|
| Wald | 6 Reihen mit je 13–23 Bäumen im festen Abstand (≈ 108) plus bis zu 95 Zufallsbäume, Reihen quer über den Weg | grüner Teppich, Bäume **auf dem Weg**, keine Lücken, keine Tiefe |
| „Charaktergruppe“ | 6 Bäume in **zwei diagonalen Linien** links und rechts vom Weg, Größe nur `0,88 / 1,02 / 1,16` im Wechsel | ein Tor, keine Gruppe |
| Baumform | Stamm plus 3–4 **glatt schattierte Kugeln** (gemittelte Normalen) | „Kugeln auf Stäben“, nichts ist facettiert |
| Gras | 175 Büschel gleichmäßig zufällig in 34 × 56 m, ab 2 m vor der Kamera | gleichmäßig verteilt, kein offener Vordergrund |
| Blumen | 132 Blumen, je ≈ 260 Dreiecke (Blüte aus sechs Kugeln) | Mikrodetail ohne Nutzen, ≈ 35.000 Dreiecke |
| Gelände | Wellenlängen 140–270 m bei ≈ 1 m Höhe | praktisch flach (höchstens ≈ 2° Neigung) |
| Farben | Boden, Laub, Gras fast gleiches Grün | „ein einziges Grün“ |
| Steine | 5 Steine, Größe nur im Dreierwechsel (Maßstab 0,55 / 0,67 / 0,79), abwechselnd links und rechts entlang des Weges | kaum Größenunterschied, keine „charaktervollen“ Steine |
| Ferne | Hügel als Ellipsoide bis z = 168, Gelände endet bei 208, nichts hinter dem Betrachter | sichtbarer Geländerand, Blick zurück leer |
| Technik | `InstancedLandscape` legt **jedes Bild neue Arrays an** (GC auf der Quest); Materialien per `Shader.Find` (kann im Player fehlen); ≈ 117.000 Dreiecke | Leistungs- und Build-Risiko |

## 2. Erste Runde: die Reparatur (Komposition)

**Struktur.** Layout und Geometrie sind **reiner C#-Code ohne Unity-Objekte** (`Assets/Droply/Scripts/Layout/`). Dieselben Dateien laufen im Spiel und im Prüfwerkzeug. `LandscapeGenerator` ist nur noch die Unity-Seite. `InstancedLandscape` entfällt: Jede Schicht ist **ein verschmolzenes statisches Mesh**, keine Arbeit pro Bild, keine GC-Allokation.

1. **Vegetation ausdünnen.** Wald ≈ 190 → 89 Bäume in Gruppen (Gruppe 5, Waldkante 26, vereinfachte Gruppen 58), Gras und Blumen in Gruppen und Inseln statt gleichmäßig.
2. **Gelände.** Sanfte, entworfene Formen: Hügelchen links und rechts im Vordergrund, Kuppe unter der Baumgruppe, flache Senke, Rücken, den der Weg hinaufführt. Am Standpunkt exakt eben (Höhe 0 = Boden-Ursprung). Ein Mesh über 600 m: fein (1,25 m) dort, wo komponiert wird, grob zum Horizont.
3. **Weg.** Beginnt als schmale Zunge zu Füßen, wird 2,4 m breit (später 1,9 m), schwingt in einem S, hat unregelmäßige Ränder und einen dunkleren Randstreifen, liegt auf der gerenderten Gelände-Oberfläche. Nichts liegt darauf (Check).
4. **Bäume neu gruppiert.** Eine Charaktergruppe aus 5 ungleichen Bäumen 28–45 m links vom Weg: Kronen überlappen leicht, Stämme und Lücken bleiben sichtbar, kein Strich.
5. **Waldkante und Ferne.** Hero → Waldkante → vereinfachte Gruppen → Waldlinie (Silhouette) → drei Hügelschichten. Jede Schicht ragt über die vordere hinaus und ist blasser und blauer; kein Nebel. Im Bereich des Weges sind Linie und Hügel abgesenkt: der Blick öffnet sich über den Rücken. Rundum 360°.
6. **Quest-Technik.** Verschmolzene Meshes, Meshes nach dem Hochladen nicht mehr lesbar (Speicher), keine Lichtsonden/Reflexionssonden, **Shader per Referenz in der Szene** (sonst können sie im Player fehlen). `HeadsetPose` fordert den **Boden-Ursprung** an, damit die Augenhöhe wirklich die Höhe über dem Boden ist (die Rig-Position setzt weiterhin keine Höhe).

**Palmen:** es gibt keine. **Kegel/Weihnachtsbäume:** nicht vorhanden; Fichten sind ausgefranste, versetzte Etagen mit Unterseite (aus der Nähe geprüft).

## 3. Zweite Runde: Grafik der Quest-App

### 3.1 Befund (Vergleich mit dem Konzeptbild, in der Vorschau)

Die erste Runde war ruhig und offen, aber **matt**: ein stumpfes Olivgrün ohne Struktur, ein verschwommener Weg (Rand über ≈ 1 m verlaufen), Kronen wie Ballons mit großen glatten Flächen, kaum erkennbare Blumen (dunkel, winzig; die Blütenblätter waren falsch herum beleuchtet), kein sichtbarer Schatten, ein leerer Vordergrund ohne Tiefenhinweise, graugrüne statt blaue Ferne, Licht von links hinten (alles von vorn beleuchtet, Schatten verdeckt).

### 3.2 Pipeline: das Licht ist in die Vertexfarben eingebacken

Auf der Quest kosten echtes Licht und Schattenkarten GPU-Zeit, und das Sonnenlicht der ersten Runde (eine Schattenkarte mit 1.024 Pixeln, 60 m Reichweite) kennt keine Umgebungsverdeckung und reicht für lange Schatten nicht weit. Deshalb wird das Licht **einmal beim Bauen** berechnet und steht danach in den Vertexfarben (`Lighting.cs`, `Look.cs`):

- **Farbe vor dem Licht** (`Look.cs`): Verläufe nach Material und Lage im Körper: Kronen unten kühl und dunkel, oben warm und hell (sonnengebleichte Spitzen), Gras am Fuß dunkel, an der Spitze hell, Weg in der Mitte hell und am Rand dunkler (abgenutzt, gefleckt), Wiese in weichen Flecken (kühler Schatten, frisches Grün, warmes Sonnengrün, trockenes Strohgelb, feine Maserung), Stämme und Steine mit etwas Moos am Fuß, Birkenrinde weiß mit dunklen Zeichen.
- **Licht** (`Lighting.cs`): eine warme Sonne (28° hoch, von links und etwas von vorn: Schatten fallen nach rechts und zum Betrachter hin, die Kronen zeigen ihre helle Seite links, ihr Laub leuchtet auf der abgewandten Seite durch), Schatten aus Kugel-Verdeckern (jede Krone, jeder Busch, jeder Stein; je weiter der Verdecker, desto weicher der Rand, bei Wolken gedeckelt auf 40 m), Umgebungsverdeckung (dunkel unter Kronen und am Fuß von Stämmen und Steinen), Dreifarb-Umgebungslicht (Himmelblau oben, Grün unten: blaugrüne Schatten), Dunst zum Horizont (`Palette.HorizonColor`), sanftes Abregeln heller Flächen. Für das **Licht** wird die Neigung des Geländes verstärkt (×2,6; die Geometrie bleibt), damit jede Welle eine besonnte und eine beschattete Seite zeigt. Dünne beidseitige Flächen (Halme, Blüten) werden auf ihrer Oberseite beleuchtet, das Licht der Unterseite kommt durch das Blatt.
- **Himmel** (`Sky.cs`): eine Kuppel (R = 325 m, 1.120 Dreiecke) und eine Sonnenscheibe (2,1°) in Vertexfarben, am Horizont genau die Dunstfarbe der fernen Schichten (kein Absatz), mit einem weichen warmen Schein um die Sonne (eingebackener Verlauf, kein Bloom, kein Lens-Flare). Ersetzt das Procedural-Skybox.
- **Wolkenschatten**: eine Wolke hängt auf dem Sonnenstrahl durch einen Punkt der Wiese (`Plan.CloudAbove`), erscheint also neben der Sonne und wirft einen weichen Schatten auf die Wiese links vom Weg.
- **Unity-Seite** (`LandscapeGenerator.cs`, `Shaders/VertexColorUnlit.shader`): vier Materialien (fest, beidseitig für Halme und Blüten, Himmel, Sonne), Meshes mit Vertexfarben statt Normalen (Position + Farbe 16 Byte je Vertex, mit Normale wären es 24), **keine Lichter, keine Schattenkarten, keine Skybox**, die Kamera löscht auf die Horizontfarbe. Der Shader (URP, unlit) wandelt die sRGB-Bytes in lineare Farbe, ist SRP-Batcher-kompatibel und trägt die Makros für Single-Pass-Instanced-Stereo. MSAA 4. **Rückfall:** fehlt der Shader oder wird er nicht unterstützt, nimmt der Generator das schlichtere URP-Lit-Aussehen (eine Farbe je Material, echte Sonne, Skybox).

### 3.3 Gestaltung (was sich sichtbar geändert hat)

- **Wiese:** wärmeres, leuchtendes Gelbgrün im Licht, kühles Blaugrün im Schatten, trockene Flecken; Korn in Blickrichtung; **Relief 3,0 → 3,6 m** (steilster Punkt 8,8° → 11,4°), im Licht verstärkt.
- **Weg:** Rand nur noch eine Handbreit (≈ 0,1 m) weich statt ≈ 1 m, wandernde Ränder, gefleckter Sand, dunklerer Randstreifen.
- **Bäume:** Eichen breiter und flacher (Laubmasse statt Ball), die großen nahen Lappen mit feinerer Facettierung (kleine kantige Blattklumpen). In der Gruppe ersetzt eine **weiße Birke** die Buche (hellster Stamm im Bild, frisches Gelbgrün). Büsche rundlicher und heller; **acht Büsche rahmen die Sicht** links und rechts in ≈ 30 m (ohne Stamm, sie schließen keine Sichtachse).
- **Gras:** spitze Halme aus je einem Dreieck (wie im Konzeptbild), 16 bewusste Gruppen (88 Büschel, bis ≈ 0,9 m), 13 Büschel am Wegrand, ≈ 1.250 kleine **Flecken** in unregelmäßigen Feldern (dünner mit der Entfernung, nie ein Teppich, nicht auf Weg und Inseln), 14 Kiesel am Wegrand. Alles hat im Nahbereich eine Aufgabe: Tiefen- und Maßstabshinweise, die der Blick zum Lesen der Bodenebene braucht.
- **Blumeninseln:** je ein grüner Hügel aus flachen Blattlappen mit Blatt-Büscheln am Rand, darüber 18–26 größere Blüten in drei Formen (Margerite, Hahnenfuß, **Lavendel-Ähre**), eine Hauptfarbe und bei den meisten eine zweite für ein Viertel der Blüten. Eine Insel liegt jetzt 14 m vor dem Betrachter. 164 Blüten auf 7 Inseln.
- **Ferne:** Hügel höher und **blau** statt graugrün, Waldlinie mit runderen Kronen (Segmente 1 m, kleine Unebenheit, wandernde Helligkeit), sieben statt fünf Wolken.

### 3.4 Messwerte nach der zweiten Runde (Rechner, nicht Quest; aktuelle Werte in 4.4 und 5.2)

| Größe | Wert |
|---|---|
| Dreiecke / Vertices / Schichten (= Zeichenaufrufe) | 72.799 / 149.728 (≈ 2,3 MiB mit Position und Farbe) / 41 |
| Bäume je Stufe (Dreiecke je Baum) | Gruppe 5 (1.680), Waldkante 26 (239), vereinfacht 58 (53) |
| Licht einbacken | ≈ 0,24 s (511 Verdecker); ganzes Bauen ≈ 0,45 s auf einem Desktop, **auf der Quest unbekannt** (das Protokoll des Generators nennt sie) |
| Offene Sichtachsen | 60 % (50 % mit den fernen Gruppen), breiteste 26°, 6 Gassen |

### 3.5 Bewusst gelockerte eigene Prüfwerte (Begründung)

Die Prüfwerte sind meine Auslegung des Reparaturprompts, nicht Vorgaben des Betreibers. Der Vordergrund war in der ersten Runde so leer, dass die Szene flach wirkte; in der VR liest das Auge die Bodenebene an nahen kleinen Dingen. Geändert (alles in `LandscapeChecks.cs`, vom `selftest` abgedeckt):

| Prüfwert | vorher | jetzt |
|---|---|---|
| Gras nicht näher als | 9 m | 6 m (Wegrandbüschel 7 m, Flecken 3,5 m, Kiesel am Weg) |
| Blumen nicht näher als | 12 m | 8 m |
| Büschel in den ersten 20 m | ≤ 12 | ≤ 18 |
| Büschel / Blumen insgesamt | ≤ 70 / ≤ 140 | ≤ 110 / ≤ 260 (zusätzlich Wegrandbüschel ≤ 30, Flecken ≤ 1.500, Kiesel ≤ 30) |
| Steilster Punkt | ≤ 9° | ≤ 12° |
| Sättigung | ≤ 0,62 für alles | ≤ 0,62 für Laub, Gras, Boden, Rinde, Stein; ≤ 0,8 für Blüten und ihre goldene Mitte |
| Bäume nicht näher als | 24 m | unverändert |

## 4. Dritte Runde: Gehen und Komfort (Phase 8)

Bis hierher war die App ein Standbild zum Umsehen. Jetzt kann man gehen; die Regeln dafür stehen in `CLAUDE.md` (21: keine erzwungene Bewegung, langsame Fortbewegung, Snap Turning) und gelten hier als Code und Prüfung.

### 4.1 Steuerung (`ViewerLocomotion.cs`, Regeln in `Walker.cs`)

- **Linker Stick: gehen**, in die Richtung, in die der Kopf schaut, höchstens **1,2 m/s** (langsames Gehen), proportional zum Ausschlag, mit toter Zone. Anfahren und Anhalten dauern **0,3 s** (höchstens 4 m/s²): kein Ruck, aber auch kein langes Beschleunigen.
- **Rechter Stick links/rechts: Snap Turn um 30°**, um den Kopf herum (das Bild verschiebt sich nicht). Eine Drehung je Druck; der Stick muss erst zurück zur Mitte, Festhalten dreht nicht weiter. Kein weiches Drehen.
- Nichts bewegt sich von selbst: keine Kamerafahrt, kein Wippen, keine Vignette. Lässt man den Stick los, steht man nach 0,3 s.
- Der **Boden des Tracking-Raums folgt dem Gelände** unter dem Kopf (weich über 0,12 s, gegen die Facetten des Geländenetzes): man geht die sanften Wellen hinauf und hinunter, die eigene Augenhöhe bleibt die echte.
- Der Kopf wird zusätzlich **direkt vor dem Rendern** gelesen (`HeadsetPose`, `Application.onBeforeRender`), das Bild nutzt so die neueste Vorhersage.

### 4.2 Wo man gehen kann (`Plan.WalkOutline`, `WalkArea.cs`)

Ein organischer Umriss um die Wiese und den Weg entlang bis zur Senke vor dem Anstieg, **rund 1.980 m²**, 71 m des Wegs sind begehbar (`ansicht-gehbereich.jpg`). Stämme, Büsche und Steine sind ausgespart (Stamm + 0,35 m für den Körper; bei der Fichte die unterste Astetage auf Kopfhöhe). Am Rand und an Stämmen gibt es keine unsichtbare Wand, an der man abprallt: in einem **Polster von 1,2 m** wird nur der Anteil der Geschwindigkeit zur Kante hin weich bis auf null zurückgenommen (0,15 m vor der Kante), der Anteil entlang der Kante bleibt. Man gleitet an Stämmen vorbei und am Rand entlang.

Der Umriss endet dort, wo die Komposition aus der Nähe nicht mehr tragen würde. Das ist gemessen (`LandscapeChecks`, Abschnitt walking):

| Prüfung | Wert |
|---|---|
| Startpunkt mindestens 1 m im Bereich, Weg mindestens 55 m begehbar | 71 m |
| alles vom Start aus erreichbar (Flutfüllung auf 1-m-Raster) | ja |
| nur auf dem feinen Geländeraster (1,25 m) | ja |
| vereinfachte Gruppen mindestens 35 m entfernt (Regel 14: 40–80 m) | nächste 42 m |
| Waldlinie (150 m) mindestens 80 m entfernt (Regel 14: Silhouetten ab 80 m) | Bereich reicht bis 66 m vom Start, also ≥ 84 m |
| **Waldkante bleibt mindestens 15 m entfernt**; was näher erreichbar ist, wird in voller Detailstufe gebaut (Regel 23: nah = volle Low-Poly-Geometrie) | die kleine Baumgruppe rechts auf der Wiese (Buche, Buche, Birke) und alle erreichbaren Büsche jetzt voll detailliert |
| nichts Sichtbares hinter der Fernebene (400 m), von jedem begehbaren Punkt | höchstens 380 m |

Der **Himmel geht mit**: Kuppel und Sonnenscheibe folgen dem Auge (sie sind „unendlich weit“), Wolken, Hügel und Waldlinie stehen fest. Die eingebackenen Abstandseffekte (Dunst ab 40 m, breitere Halme ab 20 m, weniger Flecken in der Ferne) sind vom Startpunkt aus gerechnet; im Bereich bleiben sie unauffällig (Dunst bei 60 m ≈ 2 %).

### 4.3 Geprüft außerhalb von Unity (`selftest`)

Gehen ist reiner C#-Code und läuft im Prüfwerkzeug mit 72 Bildern/s: geradeaus auf einen Stamm (hält davor, nie im Stamm, ≤ 4 m/s²), knapp daneben (gleitet vorbei), schräg in den Rand und 40 s daran entlang (nie draußen, kein Ruck), ein Bild von 2 s (kein Sprung), volle Geschwindigkeit 1,20 m/s, tote Zone, Richtung des Kopfes, Snap Turns (30°, Festhalten dreht nicht), **dem echten Weg folgen** (71 m, nie draußen) und **zehn Minuten zufälliges Umherlaufen** in der echten Landschaft (nie draußen, höchstens 4,0 m/s²). Dazu lehnt der `selftest` Gehbereiche ab, die an vereinfachte Gruppen heranreichen, dem Weg nicht folgen oder abgeschnittene Teile haben.

### 4.4 Messwerte jetzt (Rechner, nicht Quest)

| Größe | Wert |
|---|---|
| Dreiecke / Vertices / Schichten (= Zeichenaufrufe) | 75.859 / 158.908 (≈ 2,4 MiB) / 41 |
| Bäume je Stufe (Dreiecke je Baum) | Gruppe 5 (1.680), Waldkante 26 (292, drei davon voll detailliert), vereinfacht 58 (53) |
| Licht einbacken / ganzes Bauen | ≈ 0,48 s / ≈ 0,74 s auf dem heutigen Rechner (der alte Stand braucht dort ≈ 0,43 s / ≈ 0,65 s), **auf der Quest unbekannt**; seit Phase 9 schneller (5.2) |

### 4.5 Quest-Einstellungen, die gefehlt haben

In `Assets/XR/Settings/OpenXR Package Settings.asset` waren für Android **„Meta Quest Support“ und alle Controller-Profile ausgeschaltet**. Ohne Meta Quest Support startet die Android-App auf der Quest vermutlich nicht als VR-App, ohne Controller-Profil melden die Sticks nichts. Jetzt eingeschaltet: Meta Quest Support, Oculus Touch Controller Profile, Meta Quest Touch Plus Controller Profile (die Controller der Quest 3S); von Hand in der Datei und im Menübefehl `Droply → Generate and configure landscape`, die Validierung warnt, wenn eines fehlt.

## 5. Vierte Runde: Leistung, LOD und Instancing (Phase 9)

Regel 30: erst das Problem bestimmen, dann die kleinste sinnvolle Änderung. Deshalb zuerst gemessen, was die Szene kostet, dann nur das geändert, was messbar etwas bringt; der Rest ist hier mit Begründung festgehalten.

### 5.1 Befund

| Was | Wert (Rechner) | Einordnung |
|---|---|---|
| Dreiecke / Vertices pro Bild | 75.859 / 158.908 (≈ 2,4 MiB, Position + Farbe) | Metas Leitfaden nennt für die schwächere Quest 2 als Richtwert grob 750.000–1.000.000 Dreiecke und einige hundert Zeichenaufrufe pro Bild (aus dem Gedächtnis, nicht nachgeschlagen) |
| Zeichenaufrufe / Materialien / Shader | 41 / 4 / einer, ohne Licht- und Texturarbeit | keine Lichter, keine Schattenkarten, keine Skybox, keine Nachbearbeitung, MSAA 4 |
| Arbeit pro Bild auf der CPU | Gehen, Himmel folgt dem Auge | keine Speicheranforderung pro Bild (keine GC-Pausen), inkrementeller GC eingeschaltet |
| **Start** | ganzes Bauen ≈ 0,57 s, davon Licht ≈ 0,43 s (warm, bestes von 7, Rechner mit 4 Kernen) | die einzige bekannte Unbekannte: die Brille rechnet alles beim Start, vermutlich mehrfach langsamer |

Die laufende Last ist klein; was zählt, ist der Start (so lange zeigt die Brille noch keine Landschaft).

### 5.2 Geändert: der Start ist mehr als doppelt so schnell

- **Licht auf allen Kernen** (`Lighting.BakeAll`): die Schichten werden gleichzeitig gebacken, die größten zuerst; jede Schicht bäckt ein Thread ganz und in der bisherigen Reihenfolge, gelesen wird nur Unveränderliches. Deshalb sind die Farben **bitgenau dieselben** wie beim Backen auf einem Kern: der `selftest` vergleicht beide, und der Export der ganzen Szene ist byteweise gleich dem Stand vor dieser Runde. (Dafür hat jede Schicht ihre eigene Liste für die Verdecker-Suche, und die Farbtabelle in `Look` wird beim ersten Benutzen der Klasse angelegt, nicht nebenbei.)
- **Abstand zum Weg** (`PathModel.Distance`): wird beim Bauen zehntausende Male gefragt (Flecken, Gras, Blumen, Prüfungen) und ging jedes Mal alle 360 Wegstücke durch. Jetzt werden Abschnitte von 12 Stücken übersprungen, deren Rechteck sicher weiter weg ist als der beste Treffer; das Ergebnis ist dasselbe (Export gleich).
- Neuer Aufruf `dotnet run --project Tools/CompositionCheck -- bench` misst die Bauzeit (bestes von 7 nach einem Aufwärmen). Die Prüfsumme der Szene enthält jetzt auch die Farben.

| Rechner mit 4 Kernen, warm, bestes von 7 | vorher | jetzt |
|---|---|---|
| ganzes Bauen | 573 ms | **257 ms** |
| davon Licht | 429 ms | **110 ms** (auf einem Kern 418 ms) |
| davon Komposition | ≈ 144 ms | ≈ 85 ms |

Auf der Quest fehlen die Zahlen; die Startzeile im Log (`Droply landscape: … ms`) zeigt sie. Der Unity-Teil (Meshes anlegen) kommt dazu.

### 5.3 Bewusst nicht gebaut (Begründung)

- **Kacheln für Frustum Culling.** Jede Schicht ist ein Mesh rund um den Betrachter, Unity kann davon nichts wegschneiden: es werden immer alle ≈ 76.000 Dreiecke gezeichnet. Aufteilen in Kacheln würde aus 41 Zeichenaufrufen weit über hundert machen, um Vertexarbeit zu sparen, die bei dieser Menge kaum zählt. Wird erst sinnvoll, wenn die Messung in der Brille zeigt, dass die GPU an der Geometrie hängt.
- **LOD-Gruppen zur Laufzeit.** Die Detailstufen sind fest nach Abstand zum Gehbereich vergeben (Regel 23): Gruppe 1.680, Waldkante 292, vereinfacht 53 Dreiecke je Baum, dahinter Waldlinien und Hügel als Bänder. Weil der Gehbereich begrenzt ist und die Abstände geprüft werden (Waldkante ≥ 15 m, vereinfachte Gruppen ≥ 35 m, Waldlinie ≥ 80 m, 4.2), gibt es nichts umzuschalten und kein Aufploppen.
- **Instancing.** Das Licht steckt in den Vertexfarben: jede Kopie eines Baums hat eigene Schatten und eigene Verdeckung, keine zwei Instanzen wären gleich. Verschmolzene statische Meshes sind hier günstiger (41 Zeichenaufrufe, 2,4 MiB). Regel 24 sagt „wo sinnvoll“.
- **Himmel zuletzt zeichnen.** Die Himmelskuppel wird zuerst gezeichnet und überall übermalt, das kostet etwa eine Bildschirmfläche mit dem billigsten Shader. Zuletzt gezeichnet würde sie nur sichtbare Pixel kosten, dafür müsste der Shader die Kuppel auf die Fernebene legen. Das kann hier niemand übersetzen und testen; ein Fehler ließe den Himmel verschwinden. Erst mit Unity.
- **Foveated Rendering** (in OpenXR 1.18 als Funktion vorhanden, aus): spart Pixelarbeit am Bildrand, die hier fast nichts kostet, und gröbere Ränder wären an den facettierten Kanten und im Himmelsverlauf sichtbar.
- **90 statt 72 Bilder/s:** würde Gehen und Kopfdrehen glatter machen (Komfort). OpenXR 1.18 hat dafür keine eigene Einstellung (dafür bräuchte es Metas eigenes OpenXR-Paket). Kandidat, sobald die Bildrate in der Brille gemessen ist.

### 5.4 Gegen die echten Paketquellen abgeglichen (statt aus dem Gedächtnis)

Unity selbst läuft hier nicht, aber die Quelltexte der Pakete sind öffentlich (OpenXR 1.18.0 über den Paket-Spiegel von needle-mirror, URP 17.6.0 aus Unitys Graphics-Repository):

- **OpenXR-Einrichtung** (`ProjectSetup.cs`): `OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup)`, `GetFeatures()` und `feature.enabled` gibt es so; die Klassennamen `MetaQuestFeature`, `OculusTouchControllerProfile`, `MetaQuestTouchPlusControllerProfile` stimmen.
- **Sticks:** beide Controller-Profile melden den Stick unter „Primary2DAxis“, und OpenXR meldet die Aktionen auch dann an, wenn das neue Input System nicht aktiv ist (das Projekt steht auf dem alten Input Manager). `CommonUsages.primary2DAxis` in `ViewerLocomotion` ist damit der richtige Weg.
- **OpenXR-Einstellungsdatei:** die eingeschalteten Funktionen gehören zu dem Android-Satz, den das Paket wirklich benutzt. Die Datei enthält noch einen zweiten, nicht verwendeten Satz; harmlos.
- **Shader:** mit den echten URP-17.6.0-Dateien für die vier Varianten vorverarbeitet, die auf der Quest vorkommen können (ohne Stereo, Instancing, Multiview, Single-Pass-Instanced): alle Includes gefunden, die Makros ergeben den erwarteten Code (bei Multiview kommt der Augenindex aus `gl_ViewID`). Ganz übersetzt werden konnte er nicht: glslang scheitert schon an der URP-Bibliothek selbst (Überladungen mit halber Genauigkeit), Unitys eigener Compiler und DXC sind hier nicht verfügbar.

### 5.5 Messen in der Brille (Phase 10)

- **Start:** `adb logcat -s Unity`, Zeile `Droply landscape: …` (Bauen, Licht, Objekte in ms).
- **Bildrate, CPU, GPU, Wärme:** Metas *OVR Metrics Tool* (Einblendung in der Brille), ohne Code. Ziel: 72 Bilder/s ohne Einbrüche, auch beim Gehen, beim Snap Turn und mit Blick in die Baumgruppe.
- Erst wenn dort etwas fehlt, kommen die Punkte aus 5.3 wieder auf den Tisch, in dieser Reihenfolge: was die Messung als Engpass zeigt.

## 6. Abschlusskontrolle (die 20 Punkte des Prompts)

„gemessen“ = ein Check in `LandscapeChecks` (läuft in Unity unter *Droply → Validate landscape* und außerhalb mit `dotnet run --project Tools/CompositionCheck`). „gesehen“ = in der Vorschau (three.js, gebackene Vertexfarben) angesehen, nicht in Unity. „Brille“ = nur in der Quest beurteilbar.

| Punkt | Stand |
|---|---|
| Vordergrund ist offen | gemessen (Bäume ≥ 24 m, Büschel ≥ 6 m, ≤ 18 Büschel in 20 m), gesehen: nichts versperrt den Blick, aber Tiefenhinweise am Boden |
| Weg ist klar sichtbar | gemessen (nichts darauf), gesehen; Brille |
| Terrain ist nicht flach | gemessen (Relief 3,6 m, ≤ 11,4°, Standpunkt eben, Meshabweichung 0,08 m), gesehen (im Licht verstärkt) |
| Bäume gruppiert, nicht zufällig | gemessen (3–7, nicht auf einer Linie, Kronen überlappen leicht), gesehen |
| Keine Kegel-/Weihnachtsbaumformen | gesehen (Nahaufnahmen) |
| Keine tropischen Palmenformen | gemessen (keine Palmenschicht), gesehen |
| Waldkante hat Tiefenstaffelung | gemessen (Detail fällt: 1.680 > 292 > 53 Dreiecke je Baum; beim Gehen bleibt die Waldkante ≥ 15 m, vereinfachte Gruppen ≥ 35 m entfernt), gesehen |
| Ferne ist deutlich einfacher | gesehen; Brille |
| Blumen gruppiert | gemessen (5–10 Inseln, ≥ 8 m vom Betrachter), gesehen |
| Gras reduziert | gemessen (88 Büschel in 16 Gruppen, Wegrand 13, Flecken 1.250 in Feldern, alle unter ihren Grenzen), gesehen: Gruppen und Felder, kein Teppich |
| Freie Flächen vorhanden | gemessen (offene Sichtachsen 60 %, breiteste 26°; mit den fernen Gruppen 50 %) und gesehen |
| Farben nicht nur Grün | gemessen (Bodentöne, Sättigung) und gesehen: Gelbgrün, Blaugrün, Sand, Blau, Lavendel, Gelb, Weiß |
| Schatten sind weich | gemessen (Schattenwerte am Boden mit `probe`), gesehen (Vorschau); **Brille** |
| Keine störenden Kameraeffekte | Einstellung geprüft (keine Nachbearbeitung, kein Nebel, kein Bloom, keine Vignette); der Sonnenschein ist ein Verlauf im Himmel, kein Effekt; Brille |
| Maßstab wirkt menschlich | gemessen (1 Einheit = 1 m, Baumhöhen 4–12 m); Brille |
| VR-Perspektive wirkt natürlich | **Brille** (Boden-Ursprung angefordert, Boden folgt dem Gelände, Gehen und Snap Turn außerhalb von Unity simuliert; nichts davon in der Brille geprüft) |
| Keine unnötigen Mikrodetails | Dreiecke je Blume ≈ 20–35, je Fleck ≈ 6, je Kiesel 20; im Budget; die kleinen Dinge dienen dem Tiefenhinweis (3.3) |
| Quest-3S-Performance bleibt Priorität | Budget gemessen (≤ 80.000 Dreiecke, ≤ 60 Schichten, ein trivialer Shader), Start auf dem Rechner mehr als halbiert (5.2), **Bildrate und Bauzeit in der Brille nicht gemessen** |
| Wirkt nicht wie ein zufälliger Generator | gemessen (keine Reihen, kein Teppich; `selftest` lehnt die alte Anordnung und Teppiche ab), gesehen |
| Wirkt wie eine bewusst gestaltete Landschaft | **Urteil in der Brille** |

## 7. Vorschau (three.js, nicht Unitys Renderer)

`docs/vorschau/`: `ansicht-geradeaus.jpg`, `-links` (Sonne und Gruppe), `-rechts`, `-zurueck`, `-lageplan` (Baumstufen als Kreise: rot Gruppe, orange Waldkante, violett vereinfacht; weiße Punkte der Weg), `-baumgruppe`, `-waldkante`, `-blumeninsel`, `-weg`, dazu vom Gehen: `-gehbereich` (Umriss schwarz, ausgesparte Stämme, Büsche und Steine als graue Ringe), `-unter-der-gruppe` (zwischen den Stämmen der Gruppe), `-rueckblick` (vom Ende des Bereichs zurück zum Start). Die Vorschau zeichnet die gebackenen Vertexfarben ohne Licht und mit derselben Umrechnung wie der Shader, Unterschiede zur Brille sind Kantenglättung, Anzeige und Optik. Aufnahme 1600 × 900 aus Augenhöhe 1,65 m, vertikal 70°, mit den Adressen (`Tools/Preview`, `python3 -m http.server`):

```
geradeaus   index.html?yaw=0&pitch=0                                  links     ?yaw=-45&pitch=2
rechts      ?yaw=45&pitch=2                                           zurueck   ?yaw=180&pitch=2
lageplan    ?view=top&half=100&cz=48                                  weg       ?tx=1&ty=0&tz=8&px=-1&pz=2&eye=1.65&fov=55&pitch=-12
baumgruppe  ?tx=-10&ty=4.5&tz=36&px=-1&pz=18&eye=1.65&fov=62          blumeninsel  ?tx=-6.4&ty=0.5&tz=12.6&px=-3&pz=7&eye=1.65&fov=55
waldkante   ?tx=40&ty=5&tz=62&px=0&pz=0&eye=1.65&fov=42&yaw=0
gehbereich  ?view=top&half=42&cz=28                                   unter-der-gruppe  ?px=-1.5&pz=31&yaw=-62&pitch=6&fov=80
rueckblick  ?px=-2&pz=63&yaw=180&pitch=2
```

(`eye` ist die Höhe über dem Gelände an `px`/`pz`. In der Vorschau geht man mit W A S D, ohne Begrenzung; den Gehbereich hat nur die App.)

## 8. Nicht geprüft / bewusst offen

- **Alles in Unity und an der Quest** (siehe `README.md`, Abschnitt „What has not been verified“). Der Spielcode kompiliert gegen die echten UnityEngine-Assemblies (2021.3-Schnittstelle, `Tools/CompileCheck`); der Shader wurde auf Syntax geprüft und mit den echten URP-17.6.0-Dateien vorverarbeitet (5.4), **nicht von Unity übersetzt**; `ProjectSetup.cs` braucht UnityEditor, URP und XR und wurde nur gelesen (die neuen OpenXR-Aufrufe sind gegen den Quelltext von OpenXR 1.18.0 abgeglichen, 5.4, aber nicht übersetzt). Szene, Grafikeinstellungen und OpenXR-Einstellungen wurden von Hand geändert (Shader, Tracking-Raum mit `ViewerLocomotion`, Quest-Funktionen); der Menübefehl schreibt sie sauber neu.
- Die **Bauzeit auf der Quest** ist unbekannt (Rechner mit 4 Kernen jetzt ≈ 0,26 s, vorher ≈ 0,57 s; 5.2). Wenn sie noch stört: die fernen Schichten gröber backen oder das fertige Ergebnis im Editor als Mesh speichern (dann rechnet die Brille beim Start nichts mehr).
- **Sonnenschein und Sonnenscheibe** (2,1°) stehen links vorn, knapp außerhalb des Blickfelds beim Blick geradeaus; Größe und Stärke sind Geschmack (`Sky.cs`). **Himmelsverlauf:** Streifenbildung (Banding) auf den 8-Bit-Displays ist möglich; ein leichtes Rauschen im Shader würde sie verdecken (nicht gebaut).
- Die Wolken sind flach gefärbt (zweifarbig nach Flächenrichtung), keine Beleuchtung; nur die eine Wolke neben der Sonne wirft Schatten.
- Rücken und Gruppe sind für die Blickrichtung +z komponiert; hinter dem Betrachter ist die Wiese bewusst ruhig (Hügel, Waldlinie, zwei ferne Gruppen).
- **Gehen nicht in der Brille geprüft:** ob 1,2 m/s, 0,3 s Anfahren, 30° Snap Turn und das Polster am Rand angenehm sind, ob die Sticks über die Profile wirklich ankommen (`InputDevices`, `CommonUsages.primary2DAxis`) und ob der Boden beim Gehen ruhig wirkt, kann nur jemand in der Quest beurteilen. Teleportation (in den Regeln optional) ist nicht gebaut. Am Ende des Bereichs führt der Weg weiter über den Rücken; dort hält man weich an, ohne sichtbaren Grund.
- Bewegung (Gras im Wind, Vögel, Schwanken): nicht gebaut (Regeln: keine unnötigen Details, kein erzwungenes Bewegtbild).
