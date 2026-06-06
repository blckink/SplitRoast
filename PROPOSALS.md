# Vorschläge für neue Features: Nucleus Co-op

Basierend auf der Analyse des Repositories und dem Ziel, Nucleus Co-op weiterhin **sehr benutzerfreundlich, selbsterklärend und möglichst automatisiert** zu gestalten, sind hier einige sinnvolle Feature-Vorschläge:

## 1. Automatische Spielerkennung & Launcher-Integration (Auto-Discovery)
**Beschreibung:** Aktuell müssen Spiele oft manuell hinzugefügt oder gesucht werden. Ein Hintergrund-Scan könnte bekannte Spielverzeichnisse (Steam, Epic Games, GOG, Xbox App) automatisch nach installierten und unterstützten Spielen durchsuchen.
**Nutzen:**
- *Automatisierung:* Spiele tauchen direkt nach dem Start in der Liste auf.
- *Benutzerfreundlichkeit:* Keine manuelle Dateisuche (z.B. über den SearchDisksForm) mehr nötig, ideal für Einsteiger.

## 2. Intelligente "Ein-Klick" Controller-Zuweisung (Smart Input Assignment)
**Beschreibung:** Ein System, das angeschlossene Gamepads (Xbox, PlayStation, Generic) automatisch den verschiedenen Spiel-Instanzen zuweist. Wenn 4 Controller angeschlossen sind und ein 4-Spieler-Layout gewählt wird, ordnet das Tool diese sofort den Fenstern zu.
**Nutzen:**
- *Selbsterklärend:* Erspart das mühsame manuelle Zuweisen von Controllern zu Fenstern, was oft eine Fehlerquelle ist.
- *Automatisierung:* "Plug & Play" Erlebnis wie bei herkömmlichen Konsolen.

## 3. Integrierter Diagnose- & Problemlösungsassistent (One-Click Diagnostics)
**Beschreibung:** Wenn ein Spiel nicht richtig startet (z.B. wegen fehlender Abhängigkeiten, oder weil ein Mutex-Kill fehlschlägt), erkennt das Tool dies automatisch und schlägt eine "Ein-Klick-Reparatur" vor (z.B. Herunterladen fehlender Runtimes, automatisches Schließen störender Hintergrundprozesse).
**Nutzen:**
- *Benutzerfreundlichkeit:* Abstürze und Fehler sind weniger frustrierend, wenn das Tool die Lösung gleich mitliefert.
- *Selbsterklärend:* Nutzer müssen keine `crashlog.txt` Dateien mehr wälzen.

## 4. Automatisches Handler-Management & Updates
**Beschreibung:** Game-Handler (Skripte, die Spiele kompatibel machen) sollten sich automatisch im Hintergrund updaten, wenn eine stabilere Version in der Datenbank verfügbar ist. Zudem könnte das Tool für bereits installierte Spiele direkt anzeigen: *"Dieses Spiel wird jetzt unterstützt – Handler direkt mit einem Klick herunterladen?"*
**Nutzen:**
- *Automatisierung:* Immer die besten Einstellungen und Skripte ohne manuelles Suchen und Aktualisieren.

## 5. Visueller Drag & Drop Layout-Editor
**Beschreibung:** Ein komplett visuelles Interface zum Anordnen der Bildschirme. Anstatt nur aus vorgegebenen Rastern zu wählen, können Nutzer Fenster-Bereiche per Drag & Drop auf einem grafischen Abbild ihrer physischen Monitore platzieren. "Snap-to-Grid" hilft bei der perfekten Ausrichtung.
**Nutzen:**
- *Selbsterklärend:* Visuelles Feedback ist weitaus intuitiver als Dropdown-Menüs oder Checkboxen, insbesondere bei komplexen Multi-Monitor-Setups.

## 6. Lokales Savegame-Management & Auto-Backup
**Beschreibung:** Da Nucleus Co-op mit Symlinks und geklonten Instanzen arbeitet, könnten die Savegames der einzelnen Instanzen (z.B. Player 2, Player 3) automatisch nach dem Spielen in einem leicht verständlichen Archiv gesichert werden, um Profil-Korruptionen vorzubeugen.
**Nutzen:**
- *Benutzerfreundlichkeit:* Nimmt die Angst vor Datenverlust und macht das System robuster gegen Abstürze.

---
**Fazit:** Der Kernfokus liegt darauf, technische Hürden (Pfade suchen, Inputs mappen, Logs analysieren) weiter zu abstrahieren. Der Nutzer sollte idealerweise mit nur wenigen Klicks von "App starten" zu "Spiel im Splitscreen spielen" gelangen.