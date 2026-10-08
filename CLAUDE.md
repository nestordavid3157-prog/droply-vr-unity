# CLAUDE.md — FUNDAMENTALE PROJEKTREGELN

## META QUEST 3S – NATIVE, LOKALE VR-LANDSCHAFT (Unity 6, OpenXR)

**DIESE REGELN SIND FUNDAMENTAL.** Sie gelten für das gesamte Projekt und haben Vorrang vor Bequemlichkeit, schnellen Prototypen, generischen Assets, automatischen Generatoren und technischen Abkürzungen. Widerspricht eine spätere Anweisung diesen Regeln, haben diese Regeln Vorrang.

Das Projekt entsteht ausschließlich mit dem Ziel, eine hochwertige, ruhige, begehbare und lokal laufende VR-Landschaft auf der Meta Quest 3S zu erzeugen. Die Quest 3S ist nicht das Ziel einer späteren Portierung, sondern von Anfang an die Zielplattform.

**Konzeptbild (Referenz für Stimmung, Staffelung, Farben, Weg, Bäume, Blumeninseln, Steine):** `docs/konzept-landschaft.png`. Es ist ein Leitbild, keine Vorlage zum Nachbauen: Formen werden nicht kopiert, Farben nur gemessen übernommen.

---

## 1. Projektziel
Eine **native VR-Anwendung für Meta Quest 3S**, in der der Benutzer sich in einer ruhigen, stilisierten Naturwelt bewegt. Sie soll sich anfühlen wie eine hochwertige, ruhige, natürliche, begehbare, stilisierte **Low-Poly-Spielwelt** – nicht wie ein technischer Prototyp, eine zufällig generierte Landschaft, eine Desktop-3D-Szene, eine Browser-Demo, eine VR-Spielwiese oder eine fotorealistische Simulation. Sie muss aus der echten VR-Perspektive überzeugen.

## 2. Die Quest 3S ist der verbindliche Maßstab
Alle Entscheidungen gelten der echten Hardware: Standalone-VR (Android), lokale Berechnung, stabile Framerate, niedrige Latenz, kontrollierte GPU-/CPU-Last, Speicher und thermische Belastung, komfortable Bewegung, korrektes Tracking und räumliche Wahrnehmung. Was am PC hervorragend aussieht, auf der Quest aber schlecht läuft, ist kein gutes Ergebnis. **Qualitätskriterium ist die Ausführung auf der Quest 3S, nicht der Unity-Editor.**

## 3. Native VR, kein PC-First-Design
Nicht: PC-Szene → Desktop-Spiel → später VR → später Optimierung.
Sondern: **Quest 3S → native VR → Performance → Komfort → Gestaltung → zusätzliche Details.**
Web-Technologien bestimmen nicht die Architektur. Eine Web-Version kann separat existieren; die native Quest-Anwendung ist das Hauptprodukt.

## 4. Lokal und offline
Nicht erforderlich sein dürfen: Cloud-Rendering, Streaming vom PC, ein laufender Gaming-PC, dauernde Internetverbindung, externe Server, Online-Datenbank, Cloud-Abhängigkeit. Szenendaten, Assets und Laufzeitinformationen liegen lokal. Was ohne Internet funktioniert, ist zu bevorzugen.

## 5. VR ist die primäre Perspektive
Der Benutzer steht in der Welt. Alles wird aus der menschlichen Perspektive beurteilt: Augenhöhe, Größenverhältnisse, Entfernung, Sichtachsen, Bodenhöhe, Wegbreite, Baum- und Vegetationshöhe, Tiefe, Vorder-/Mittel-/Hintergrund. **Keine** Fly-Camera, Third-Person-Kamera, schwebende Kamera oder automatische Kamerafahrt. Die Kopfbewegung bestimmt die Kamera.

## 6. Realer Maßstab
**1 Unity Unit ≈ 1 Meter.** Keine künstliche Verkleinerung, keine übergroßen Blumen, Gräser, Steine, Bäume, Wege. Keine Miniatur- oder Spielzeugwelt. Es soll sich anfühlen, als stünde ein Mensch darin.

## 7. Visuelle Identität
Ruhige Low-Poly-Naturwelt: große ruhige Farbflächen, facettierte Formen, einfache hochwertige Geometrie, natürliche Proportionen, sanfte Geländeformen, klare räumliche Staffelung, warme seitliche Sonne, weiche Schatten, blau-grüne Schattenbereiche, natürliche Grüntöne, warme Grasflächen, sandfarbener Weg, blauer Himmel, wenige weiche Wolken, türkis-bläuliche atmosphärische Ferne. Stilisiert, aber natürlich – **nicht fotorealistisch**.

## 8. Bewusst gestaltet, nicht zufällig
**Prozedurale Systeme dürfen die Gestaltung unterstützen, aber nie ersetzen.** Nicht einfach Objekte, Bäume, Gras, Blumen zufällig oder gleichmäßig verteilen, keine Wälder als Objektwolke. Die sichtbaren Bereiche werden komponiert. Zufall nur für kontrollierte Variation: Baumgröße, Rotation, kleine Farbvariationen, Grasformen, natürliche Unregelmäßigkeit – nie als Ersatz für Leveldesign. Fester Startwert für alles Platzieren.

## 9. Räumliche Hierarchie
- **Vordergrund:** Gras, Steine, Blumeninseln, Wegkante.
- **Mittelgrund:** Baumgruppen, Waldkante, Geländeformen.
- **Hintergrund:** vereinfachte Bäume, entfernte Waldschichten, Hügel.
- **Ferne:** blau-grüne Landschaft, atmosphärische Baum-/Hügelschichten, Himmel.

Die Detaildichte ist nicht überall gleich.

## 10. Terrain
Nie eine flache Ebene: leichte Bodenwellen, sanfte Erhöhungen und Senken, natürliche Übergänge, subtile Höhenunterschiede. Keine extremen Berge vor dem Benutzer, nichts Übertriebenes. Ruhig und begehbar.

## 11. Der Weg
Orientierungselement: ca. **2–3 m breit**, sanft geschwungen, warm sandfarben, organische Ränder, natürlich eingefügt. Keine harte geometrische Straße, keine gerade Linie, keine Betonoptik, keine scharfe Grenze zum Gras. Er führt den Benutzer sanft durch die Landschaft.

## 12. Bäume – keine Kegel (absolute Regel)
Nie Kegel oder Weihnachtsbäume. Ein Baum hat sichtbaren Stamm, sichtbare Hauptäste, unregelmäßige Verzweigung, mehrere Kronenbereiche, unterschiedlich große Kronencluster, natürliche Überlappung. Krone aus unregelmäßigen Low-Poly-Formen: ca. **5–12 Kronencluster** je charakteristischem Baum (nach Größe und Entfernung), nicht identisch. Keine Kegel-, Kugelbäume, identischen Kopien oder perfekt symmetrischen Kronen.

## 13. Baumvariation
Unterschiede in Höhe, Stammstärke, Stammneigung, Aststruktur, Kronengröße/-position, Rotation, Farbnuance, Silhouette – kontrolliert. Nicht jeder Baum völlig anders; die Welt behält eine gemeinsame visuelle Sprache.

## 14. Waldkante
Organisch, keine „Wand aus Bäumen“: einzelne Bäume, kleine Gruppen, Zwischenräume, unterschiedliche Höhen, Überlappungen, sichtbare Stämme, gestaffelte Tiefen.
**15–40 m:** erkennbare Bäume · **40–80 m:** vereinfachte Baumgruppen · **80–150 m:** Silhouetten · danach: atmosphärische Landschaftsschichten.

## 15. Gras
Überwuchert die Landschaft nicht; der Vordergrund bleibt offen. Keine riesigen Büschel, Graswände, meterhohen Halme, dichten Teppiche vor der Kamera. Kontrollierte Gruppen mit unterschiedlichen Höhen, Formen, Dichten, Rotationen – ohne sichtbare Wiederholungsmuster.

## 16. Blumen
Nicht gleichmäßig verteilt, sondern **ca. 5–10 klar erkennbare kleine Inseln** mit unterschiedlichen Größen, Formen und kleinen Farbgruppen. Gestaltungselement, kein Partikelrauschen.

## 17. Steine
Bewusst gruppiert; im Vordergrund einige größere, charaktervolle Steine: Low-Poly, facettiert, verschieden groß, leicht unterschiedlich gedreht, natürlich im Gelände sitzend. Keine identischen Steine in Reihen.

## 18. Licht
Warme seitliche Sonne: warme Lichtseite, weichere Schattenseite, **blau-grüne Schatten**, lange weiche Schatten, ruhige Kontraste. Kein schwarzer Schatten, keine harten Kontraste, kein übertriebenes HDR, **kein Bloom, kein Lens Flare**, keine künstlichen Filmeffekte. Quest-tauglich performant.

## 19. Himmel
Ruhig und klar: klares Blau, wenige weiche abgerundete Wolken, keine dramatische Wetterstimmung, keine Gewitterwolken. Die Ferne darf blau-grün und leicht atmosphärisch sein – **keine künstliche Nebelwand**.

## 20. Absolute visuelle No-Gos
Häuser · Gebäude · Architektur · technische Objekte · Geräte · Straßenlaternen · Zäune ohne gestalterischen Grund · Kegelbäume · Weihnachtsbaum-Look · flaches Terrain · riesige Graswände · zufällige Baumreihen · sichtbare Wiederholungsmuster · gleichmäßig verteilte Vegetation · sterile Standard-Assets · Fotorealismus · übertriebene Partikel · Mikrodetails ohne Nutzen · schwarzer Schatten · Bloom · Lens Flare · Motion Blur · starke Vignette · extreme Tiefenunschärfe · künstliche Nebelwand · extreme Kontraste.

## 21. VR-Komfort (nicht optional)
Keine erzwungene Bewegung: keine automatische Kamerafahrt, künstliche Kameraschwingung, kein Head-Bobbing, keine erzwungenen Drehungen, schnellen automatischen Bewegungen, plötzliche Beschleunigung oder Kamerapositionierung. Vorgesehen: langsame Fortbewegung, Snap Turning, optional Teleportation. Der Benutzer behält jederzeit die Kontrolle über seine Perspektive.

## 22. Performance ist Teil des Designs
Jedes Asset wird von Anfang an quest-tauglich gebaut. Vor jeder neuen Funktion oder jedem Asset bedenken: Polygonanzahl, Draw Calls, Materialanzahl, Texturgröße, Shader-Komplexität, Schattenkosten, Sichtbarkeit, CPU-/GPU-Kosten, Speicher.

## 23. LOD
Nah: volle Low-Poly-Geometrie · Mittel: vereinfacht · Fern: Silhouette/vereinfachte Baumgruppe · Sehr fern: Landschaftsschicht/atmosphärische Form. Keine unnötig komplexen Objekte kilometerweit.

## 24. Instancing und Wiederverwendung
Wo sinnvoll Instancing und wiederverwendbare Meshes – aber nie auf Kosten sichtbarer Monotonie: ein Asset wird technisch wiederverwendet und wirkt durch kontrollierte Variation natürlich.

## 25. Sichtbarkeit
Frustum Culling, LOD, sinnvolle Sichtweiten, vereinfachte Fernobjekte, kontrollierte Schattenreichweite, Instancing. Die Landschaft wirkt durch Komposition groß, nicht durch maximale Renderdistanz.

## 26. Qualität vor Quantität
Ein hochwertiger Baum schlägt 100 schlechte. Eine gute Waldkante schlägt einen zufälligen Waldgenerator. Eine kleine Blumeninsel schlägt tausende zufällige Blumen. Ein sauberer Vordergrund schlägt maximale Objektdichte.

## 27. Schrittweise Entwicklung (zwingend in Phasen)
1. Terrain + Weg + VR-Kamera
2. Ein hochwertiger Baum
3. Mehrere Baumvariationen
4. Waldkante
5. Gras + Steine
6. Blumeninseln
7. Beleuchtung + Himmel + Farben
8. VR-Bewegung + Komfort
9. LOD + Instancing + Performance
10. Feinschliff auf echter Quest 3S

Nie die ganze Landschaft in einem Schritt erzeugen.

## 28. Nicht zu früh weitermachen
Sieht die vorherige Phase schlecht aus, nicht zur nächsten springen. Bäume wie Kegel → nicht mit Blumen weitermachen. Terrain flach → nicht den Wald bauen. Quest ruckelt → keine weiteren Details. Erst das grundlegende Problem beheben.

## 29. Echte Quest-3S-Tests
Regelmäßig auf der echten Quest 3S, der Editor reicht nicht. Prüfen: Framerate, Tracking, Bewegung, Ladezeiten, visuelle Qualität, Komfort, Schatten, LOD-Übergänge, Draw Calls, GPU-/CPU-Auslastung, Speicher, thermisches Verhalten. **Was nicht in der Brille geprüft ist, wird als „nicht am Gerät geprüft“ benannt – nie als geprüft ausgegeben.**

## 30. Debugging
Nicht sofort neue Systeme. 1. Problem eindeutig identifizieren · 2. Ursache bestimmen · 3. kleinste sinnvolle Änderung · 4. auf Quest 3S testen · 5. visuell prüfen · 6. erst dann weiter. Keine unnötige Komplexität.

## 31. Keine technischen Abkürzungen auf Kosten der Vision
Nie „das ist einfacher“ als Grund für eine schlechte Lösung: kein Kegel statt Baum, keine Ebene statt Terrain, keine Baumreihen statt Waldgestaltung, keine Graswüste statt kontrollierter Vegetation, keine Standardmaterialien statt passender Farben. Technische Vereinfachung ist erlaubt, wenn sie unsichtbar bleibt und die Qualität nicht zerstört.

## 32. Fertig ist nicht gleich „funktioniert“
Nicht fertig, nur weil die Szene startet, die Quest sie lädt, man laufen kann, Bäume oder Gras existieren. Ein Blockout ist kein Produkt. **Fertig** heißt: technisch stabil, performant, komfortabel, räumlich überzeugend, visuell konsistent, bewusst gestaltet, auf der echten Quest 3S getestet.

## 33. Die wichtigste Qualitätsfrage
Nach jedem größeren Schritt: *„Wenn ich jetzt die Quest 3S aufsetze und mitten in dieser Landschaft stehe – fühlt sich das wie eine bewusst gestaltete, ruhige, hochwertige Naturwelt an oder wie ein technischer Prototyp?“* Lautet die Antwort „Prototyp“, werden nicht weitere Objekte hinzugefügt, sondern die bestehende Gestaltung verbessert (zuerst Platzierung, Maßstab, Silhouette, Licht).

## 34. Goldene Regel
Die Quest 3S ist nicht das Gerät, auf das später portiert wird – sie ist das Gerät, für das von Anfang an gebaut wird. Native VR, lokale Ausführung, Performance, Komfort und visuelle Qualität sind gemeinsame Grundanforderungen; keine darf zugunsten einer anderen ganz geopfert werden.

## 35. Absolute oberste Regel
**BAUE KEINE GENERISCHE 3D-LANDSCHAFT. BAUE EINE BEWUSST GESTALTETE VR-WELT FÜR DIE META QUEST 3S.**
Jede Zeile Code, jedes Asset, Material, Terrainstück, jeder Baum, jede Vegetationsgruppe und jede technische Entscheidung dient dieser Vorgabe.
