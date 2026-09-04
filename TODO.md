# TODO — Actos.Client (dotnet) feature-parity & polish

> Hedef: node/kotlin SDK'larıyla **tam yüzey paritesi**. Şu an tüm belgelenen API
> erişilebilir durumda; aşağıdakiler konfor/eksik kolaylık parçalarıdır (client
> geliştirmeyi engellemez — her biri `Page<T>.NextCursor` / `RateLimit` üzerinden
> elle türetilebilir).
>
> Öncelik: düşük. Yalnızca dotnet'i yüzey-paritesine çekmek istediğimizde ele alınır.

## 1. `InboxResource.watch()` — yoklama yardımcısı (tek gerçek eksik)
- node/kotlin/rust/cli'da var; dotnet'te yalnızca `ListAsync` / `ReadAsync` / `ReadAllAsync`.
- Push/SSE yok — bu bir **yoklama döngüsü** olmalı (`IAsyncEnumerable` / periyodik `ListAsync`),
  `RateLimit` + `Retry-After` header'larına uymalı, `CancellationToken` ile durmalı.
- `UnreadCount` toplam sayıdır (sayfa boyutu değil — `InboxResponse.UnreadCount` zaten doğru).
- Test: yoklama aralığı, dedupe (aynı id bir kez), rate-limit'e saygı.

## 2. İkincil listelerde `StreamAsync` eksik
- `StreamAsync` mevcut: `Actors`, `Feed` (list+following), `Comments`.
- **Eksik** (node `iterate()` buna denk): `Tags.ListAsync/PostsAsync`, `Search.ContentSearchAsync`,
  `Saves.SavesAsync`, `Admin.ReportsAsync/ActionsAsync`, `Inbox.ListAsync`.
- Desen hazır (`PageExtensions.StreamAsync(Func<PageOptions,Task<Page<T>>>)`), her biri için ince ek.

## 3. İsteğe bağlı parite / polish
- `Inbox.UnreadCountAsync()` kolaylığı (`list(limit:1)`'den türetme) — ayrı uç yok.
- `Votes.MyVotesAsync` & benzerlerinde 100-kapak behavior testlerini genişlet (şu an kodda var, test yetersiz olabilir).
- `Uploads.UploadAsync(Stream)` retry-rewind: stream zaten belleğe buffere alınıyor — belgele; belleğe alma büyük dosyalar için not düş.
- XML-doc kapsamını biraz genişlet (node/kotlin kadar dokümante değil — davranış aynı).

## Yapılmaması gereken (kontrattan bilinçli)
- `verifications.*` — backend NOT (SSRF/TOCTOU) gerekçesiyle v1 kapsamı dışı; ekleme.
- Bloklayan cephe — C# async native, `Task` yeterli. (`ActosClientOptions.HttpClient` ile senkron kullanılabilir.)

---
Kapılar: her ek için `dotnet build` (0/0) + `dotnet test` (74) + ilgili yeni test. Commit'ler ayrı, local.