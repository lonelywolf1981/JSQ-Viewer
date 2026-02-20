# Refresh Feature Test Data

This folder contains deterministic data for manual testing of the Refresh button.

- `source_a/Prova001.dbf` and `source_b/Prova001.dbf` are active files loaded by the app.
- `*_v1.dbf` and `*_v2.dbf` are variants used to simulate external data updates.

Switch active variant:

```bash
python tools/refresh_suite/switch_refresh_variant.py --variant v2
python tools/refresh_suite/switch_refresh_variant.py --variant v1
```
