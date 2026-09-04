# Yapılacaklar — .NET SDK

> Durum özeti. Canlı referans: `PLAN.md`. Detaylı API özeti: üst dizindeki `CONTEXT.md`.

## Özet
`dotnet/` sıfırdan 5. SDK. Kanonik kontrat = node/kotlin PLAN §2 → bu SDK izler.
Referans desen: **Kotlin** (diğer SDK'lar "referans buydu" der). Kaynak hakikat:
`actos-backend/docs/openapi.json` (45 yol).

- Proje: `Actos.Client` (net8.0 LTS). Çalışma zamanı sıfır dış bağımlılık
  (HttpClient + System.Text.Json yerleşik).
- Dış yüzey PascalCase, wire snake_case (modelde `[JsonPropertyName]`).
- 14 kaynak, sealed hata hiyerarşisi (12 kod), cursor sayfalama, otomatik idempotency,
  jitter'lı retry (429 Retry-After'a uyar), 3-durumlu avatar, yorum ağacı `body_html`.
- Lisans Apache-2.0, paket (NuGet) backend prod'a kadar yayınlanmaz.

## Faz durumu
- [x] Faz 0 — iskelet (solution, csproj, .gitignore, LICENSE, README)
- [x] Faz 1 — Transport + Json
- [x] Faz 2 — Hata hiyerarşisi
- [x] Faz 3 — Retry
- [x] Faz 4 — Sayfalama
- [x] Faz 5 — Model üretici (56 tip üretildi)
- [x] Faz 6 — Auth + Actors
- [ ] Faz 7 — Posts + Comments
- [ ] Faz 8 — Feed + Search + Tags
- [ ] Faz 9 — Votes + Saves
- [ ] Faz 10 — Uploads
- [ ] Faz 11 — Inbox
- [ ] Faz 12 — Reports + Admin
- [ ] Faz 13 — Meta + kaçış kapağı + env
- [ ] Faz 14 — Sözleşme testleri (canlı backend)
- [ ] Faz 15 — Examples + README + polish
- [ ] v1 — NuGet yayını (backend prod'a çıkınca)

## Sınırlılıklar / kararlar
- `verifications.*` YOK (backend'de ertelendi).
- `bloklayan cephe` YOK (C# async native, `Task` yeterli).
- `watch()` = yoklama (push yok).
- `unread_count` = toplam; ayrı uç yok → `ListAsync(limit:1)`'den türet.
- İlk admin/oturumlar API dışı; SDK dokunmaz.