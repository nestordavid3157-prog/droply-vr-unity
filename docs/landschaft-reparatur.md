# Reparatur der Landschaft (2026-10-07)

Auftrag: der „Reparaturprompt“ des Betreibers. Die Landschaft war ein dichter, zufällig gefüllter Low-Poly-Wald; sie soll eine bewusst komponierte, ruhige, offene Naturwelt für die Quest 3S sein. **Reparatur durch Reduktion und Neuplatzierung, nicht durch mehr Objekte.** Was bleibt: Projekt, URP-/OpenXR-Setup, Menübefehle, Szene, Ablauf „Generate → Validate“.

**Nichts davon wurde in Unity oder an einer Quest geprüft** (siehe unten, „Nicht geprüft“). Geprüft wurde mit einem Werkzeug außerhalb von Unity: derselbe C#-Code baut die Szene, Checks messen die Regeln, eine Vorschau zeigt sie aus Augenhöhe.

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

## 2. Was geändert wurde (nach den Phasen des Prompts)

**Struktur.** Layout und Geometrie sind jetzt **reiner C#-Code ohne Unity-Objekte** (`Assets/Droply/Scripts/Layout/`). Dieselben Dateien laufen im Spiel und im Prüfwerkzeug. `LandscapeGenerator` ist nur noch die Unity-Seite: Meshes, Materialien, Licht, Himmel, Kamera. `InstancedLandscape` entfällt: Jede Schicht ist **ein verschmolzenes statisches Mesh** (36 Schichten, keine Arbeit pro Bild, keine GC-Allokation).

1. **Vegetation ausdünnen.** Wald ≈ 190 → 89 Bäume in Gruppen (Gruppe 5, Waldkante 26, vereinfachte Gruppen 58), Gras 175 → 46 Büschel in 9 kleinen Gruppen, Blumen 132 → 87 in 7 Inseln, **Dreiecke ≈ 117.000 → 40.790**. Nichts näher als 9 m außer Weg und Steinen (Gras ≥ 9 m, Blumen ≥ 12 m, Bäume ≥ 24 m).
2. **Gelände.** Sanfte, entworfene Formen: Hügelchen links und rechts im Vordergrund, Kuppe unter der Baumgruppe, flache Senke, Rücken, den der Weg hinaufführt (Relief ≈ 3 m, steilster Punkt 8,8°). Am Standpunkt exakt eben (Höhe 0 = Boden-Ursprung). Ein Mesh über 600 m: fein (2 m) dort, wo komponiert wird, grob zum Horizont.
3. **Weg.** Beginnt als schmale Zunge zu Füßen, wird 2,4 m breit (später 1,9 m), schwingt in einem S, hat unregelmäßige Ränder und einen dunkleren Randstreifen, liegt auf der gerenderten Gelände-Oberfläche. Nichts liegt darauf (Check).
4. **Bäume neu gruppiert.** Eine Charaktergruppe aus 5 ungleichen Bäumen (Eiche 11 m, Eiche 8,6 m, Buche, junge Eiche, Fichte) 28–42 m links vom Weg: Kronen überlappen leicht, Stämme und Lücken bleiben sichtbar, kein Strich. Dazu 4 Büsche am Fuß.
5. **Waldkante und Ferne.** Hero (826 Dreiecke je Baum) → Waldkante (221) → vereinfachte Gruppen (53) → Waldlinie (Silhouette) → drei Hügelschichten. Jede Schicht ragt über die vordere hinaus und ist blasser und blaugrüner; kein Nebel. Im Bereich des Weges sind Linie und Hügel abgesenkt: der Blick öffnet sich über den Rücken. Rundum 360°, kein Geländerand.
6. **Farben.** Boden in drei ruhigen Tönen (warm besonnt, Wiese, kühler Schatten; im Vordergrund fast einfarbig, die Flecken wachsen mit der Entfernung), Sandweg, warm-braune Stämme, Eiche/Buche/Fichte in je drei Tönen (hell oben, blaugrüner Schatten unten), Steine warmgrau, kein Neon (Sättigung ≤ 0,62, geprüft).
7. **Blumen und Steine.** 7 Inseln (Lavendel, Weiß, Gelb), jede mit 9–14 größeren Blüten (≈ 20 Dreiecke statt ≈ 260). 6 Steine: ein großer links im Vordergrund, einer rechts als Gegengewicht, die übrigen verstreut, abgerundet, im Boden versenkt.
8. **Licht.** Warmes Seitenlicht von links, 26° hoch (lange weiche Schatten nach rechts), weiche Schatten, Trilight-Umgebung (blauer Himmel, grüner Boden) für blaugrüne Schatten. Kein Bloom, kein Nebel, keine Nachbearbeitung. Ferne Schichten und Wolken sind **Unlit** (flach, billig, unabhängig von Shader-Schlüsselwörtern).
9. **Quest-Technik.** Verschmolzene Meshes (Quest-üblich), Schatten nur für Gruppe, Waldkante und Steine (≈ 11.000 Dreiecke), Terrain/Ferne/Gras/Blumen werfen keine, Meshes nach dem Hochladen nicht mehr lesbar (Speicher), keine Lichtsonden/Reflexionssonden. **Shader per Referenz in der Szene** (sonst können sie im Player fehlen). `HeadsetPose` fordert den **Boden-Ursprung** an, damit die Augenhöhe wirklich die Höhe über dem Boden ist (die Rig-Position setzt weiterhin keine Höhe).

**Palmen:** es gibt keine. **Kegel/Weihnachtsbäume:** nicht vorhanden; Fichten sind ausgefranste, versetzte Etagen mit Unterseite (aus der Nähe geprüft).

## 3. Abschlusskontrolle (die 20 Punkte des Prompts)

„gemessen“ = ein Check in `LandscapeChecks` (läuft in Unity unter *Droply → Validate landscape* und außerhalb mit `dotnet run --project Tools/CompositionCheck`). „gesehen“ = in der Vorschau (three.js) angesehen, nicht in Unity. „Brille“ = nur in der Quest beurteilbar.

| Punkt | Stand |
|---|---|
| Vordergrund ist offen | gemessen (Gras ≥ 9 m, Blumen ≥ 12 m, Bäume ≥ 24 m, höchstens 12 Büschel in 20 m) und gesehen |
| Weg ist klar sichtbar | gemessen (nichts darauf), gesehen; Brille |
| Terrain ist nicht flach | gemessen (Relief 3,0 m, ≤ 8,8°, Standpunkt eben), gesehen |
| Bäume gruppiert, nicht zufällig | gemessen (3–7, nicht auf einer Linie, Kronen überlappen leicht), gesehen |
| Keine Kegel-/Weihnachtsbaumformen | gesehen (Nahaufnahmen) |
| Keine tropischen Palmenformen | gemessen (keine Palmenschicht), gesehen |
| Waldkante hat Tiefenstaffelung | gemessen (Detail fällt: 826 > 221 > 53 Dreiecke je Baum), gesehen |
| Ferne ist deutlich einfacher | gesehen; Brille |
| Blumen gruppiert | gemessen (5–10 Inseln, ≥ 12 m vom Betrachter) |
| Gras reduziert | gemessen (≤ 70 Büschel; 46) |
| Freie Flächen vorhanden | gemessen (offene Sichtachsen 62 %, breiteste 28°; mit den fernen Gruppen 50 %) und gesehen |
| Farben nicht nur Grün | gemessen (drei Bodentöne mit je 12–65 % der Wiese, Sättigung) und gesehen |
| Schatten sind weich | **Brille/Unity** (weiche Schatten sind eingestellt, nicht gesehen) |
| Keine störenden Kameraeffekte | Einstellung geprüft (keine Nachbearbeitung, kein Nebel, kein Bloom); Brille |
| Maßstab wirkt menschlich | gemessen (1 Einheit = 1 m, Baumhöhen 4–12 m); Brille |
| VR-Perspektive wirkt natürlich | **Brille** (Boden-Ursprung angefordert, ungeprüft) |
| Keine unnötigen Mikrodetails | Dreiecke je Blume ≈ 20, je Büschel ≈ 24, Gelände fern grob; gemessen im Budget |
| Quest-3S-Performance bleibt Priorität | Budget gemessen (≤ 80.000 Dreiecke, ≤ 26.000 mit Schatten, ≤ 60 Schichten), **Bildrate nicht gemessen** |
| Wirkt nicht wie ein zufälliger Generator | gemessen (keine Reihen, kein Teppich; `selftest` lehnt die alte Anordnung ab), gesehen |
| Wirkt wie eine bewusst gestaltete Landschaft | **Urteil in der Brille** |

## 4. Vorschau (three.js, nicht Unitys Renderer)

`docs/vorschau/`: `ansicht-geradeaus.jpg`, `-links`, `-rechts`, `-zurueck`, `-lageplan` (Baumstufen als Kreise: rot Gruppe, orange Waldkante, violett vereinfacht; weiße Punkte der Weg), `-baumgruppe`, `-waldkante`. Erzeugt aus Augenhöhe 1,65 m, 100° horizontal. Farben, Shader und Kantenglättung weichen von URP auf der Quest ab.

## 5. Nicht geprüft / bewusst offen

- **Alles in Unity und an der Quest** (siehe `README.md`, Abschnitt „What has not been verified“). Der Spielcode kompiliert gegen die echten UnityEngine-Assemblies (2021.3-Schnittstelle, `Tools/CompileCheck`); `ProjectSetup.cs` braucht UnityEditor, URP und XR und wurde nur gelesen, nicht kompiliert.
- Die Himmelsfarbe am Horizont (`Skybox/Procedural`) gegen die Dunstfarbe der fernen Schichten (`Palette.HorizonColor`) ist geschätzt; ein sichtbarer Absatz am Horizont ist möglich. Beide Werte stehen in `Palette.cs`.
- Die Wolken sind flach gefärbt (zweifarbig nach Flächenrichtung), keine Beleuchtung.
- Rücken und Gruppe sind für die Blickrichtung +z komponiert; hinter dem Betrachter ist die Wiese bewusst ruhig (Hügel, Waldlinie, zwei ferne Gruppen).
- Eine Eichen-Variante mit Schaukeln im Wind, Gräser im Wind, Vögel: nicht gebaut (Prompt: keine unnötigen Details).
