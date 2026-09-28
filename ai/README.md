# Smart Bikestation – KI-Anomalieerkennung

> Das Backend hat zusätzlich eine eingebaute, statistisch lernende Anomalieerkennung
> (robuster Z-Score, siehe `docs/ARCHITEKTUR.md`), die ohne Python läuft. Dieser Dienst ist die
> erweiterte Variante mit Machine Learning und kann parallel laufen.

Python-Dienst, der aus den gespeicherten Sensordaten lernt, wie normales Verhalten an den
Stellplätzen aussieht, und ungewöhnliche Messungen als Anomalie an die API meldet.

## Wie funktioniert die KI?

- **Modell:** Isolation Forest (scikit-learn), unüberwachtes Lernen – es braucht keine
  vorher markierten „Diebstahl“-Beispiele.
- **Merkmale pro Messung:** Druck, Abstand, Vibration, Druckänderung und Abstandsänderung
  zur vorherigen Messung desselben Platzes.
- **Idee:** Der Isolation Forest lernt die typischen Kombinationen. Normal ist z. B.
  „hoher Druck + kleiner Abstand“ (Fahrrad steht) oder „niedriger Druck + großer Abstand“ (frei).
  Auffällig ist z. B. „Vibration + Druck fällt stark + Abstand springt“ oder
  „hoher Druck, aber nichts im Abstand“ (widersprüchliche Sensoren).
- **Score:** 0 bis 1, ab 0,6 (einstellbar) wird gemeldet. Die Meldung enthält eine kurze Begründung
  (welche Merkmale am stärksten abweichen) und erscheint im Dashboard als „KI-Anomalie“.
- Das Modell wird alle 10 Minuten neu trainiert und passt sich so an neue Daten an.
- Es braucht mindestens 200 Messwerte zum Lernen. Für die Präsentation: im Admin-Bereich
  „Demo-Daten erzeugen“ klicken.

Das ist bewusst **keine** feste Regel wie „wenn Vibration, dann Alarm“ – die einfache
Vibrations-Meldung gibt es zusätzlich direkt im Backend.

## Starten

```bash
cd ai
python -m venv .venv
.venv\Scripts\activate          # Linux/Pi: source .venv/bin/activate
pip install -r requirements.txt

python anomaly_service.py --selftest                      # Modell mit künstlichen Daten prüfen
python anomaly_service.py --api http://localhost:5137 --api-key dev-geraete-key-nur-lokal
```

Der API-Key muss `Devices:ApiKey` der API entsprechen.
