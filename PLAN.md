# Actos .NET SDK — Uygulama Planı

> Bu dosya canlı bir kontrol listesidir. Bir adım bitince `[ ]` → `[x]` yapılır.
> Kural: **bir seferde bir adım.** Her adım kendi başına derlenir/çalışır ve
> kendi commit'ini alır. "Sonra toparlarız" yok.
>
> Kapsam: **`actos` .NET kütüphanesi** (`Actos.Client`). Backend ayrı repo
> (`actos-dev/backend`), bu plan onu değiştirmez.
>
> **Bu planı okuyan ajana:** §2 "SDK Sözleşmesi" bu kütüphanenin varlık
> sebebidir. Bir uygulama kararı sözleşmeyle çelişiyorsa sözleşme kazanır.
> Sözleşme node/kotlin kanonik kontratının (node & kotlin PLAN §2) .NET'e
> idiomatik uyarlamasıdır — bir maddeyi burada değiştirirsen kardeş SDK'ların
> kontratıyla da tutarlılığını gözden geçir.

---

## 0. Sabitlenmiş Kararlar (değiştirmeden önce iki kere düşün)

| Konu | Karar |
|---|---|
| Artifact | `Actos.Client` — hiçbir yere yayınlanmadı, v1'de yayın yok (backend prod'a çıkana kadar bekler) |
| Hedef | **net8.0 LTS** (geniş tüketici uyumu; mevcut makinede SDK 10.0.400 bu hedefi üretir) |
| Dil | C# modern (`nullable` referans tipler AÇIK, `ImplicitUsings` açık, `LangVersion` latest) |
| HTTP | **`System.Net.Http.HttpClient`** (yerleşik) — çalışma zamanı **sıfır dış bağımlılık** (node'un "zero-dep" etosuyla aynı) |
| Serileştirme | **System.Text.Json** (yerleşik). Wire **snake_case**, dış yüzey **PascalCase** — `[JsonPropertyName("snake_case")]` ile modelde tek yerde |
| Tipler | **`actos-backend/docs/openapi.json`'dan üretilir** (scripts/ altında küçük bir C# üretici). Elle düzenlenmez |
| Async | **`Task<T>` birincil** (C# doğal async). Bloklayan cephe **yok** (`Task` yeterli) |
| Kaynak erişimi | read-only property: `client.Auth`, `client.Posts`, `client.Inbox`, ... |
| Sayfalama | `Page<T>` tek sayfa + `NextCursor`; `StreamAsync()` → `IAsyncEnumerable<T>` şeffaf cursor takibi |
| metadata | serbest JSON — **`JsonElement`** olarak olduğu gibi taşınır, dönüştürülmez |
| Lisans | **Apache-2.0** (kardeş SDK'lar gibi; backend AGPL kalır) |
| Yayın | **v1'de yok.** NuGet yayını backend prod sonrası |
| Build | `dotnet` CLI, solution `Actos.sln`, klasik csproj |
| Lint/analiz | .editorconfig + `dotnet build` sıfır uyarı; sürekli analiz açık |
| Test | **xUnit** + stub `HttpMessageHandler` (birim) + canlı backend'e karşı ayrı sözleşme paketi |
| Hata dallanması | `code` alanına göre (`status`'e değil) — §4 |
| 429 varsayılanı | `Retry-After`'a uyup yeniden dene (en fazla `maxRetries`, varsayılan 2). CLI'ın tersi |

### 0.1. Neden SDK Apache-2.0, backend AGPL
Sözleşme özgür kalsın; SDK'ya bağlanan uygulama açılmasın. Sunucu AGPL ile korunur.

### 0.2. Spec otoritedir — backend Faz 18.A tamamlandı
Backend openapi **45 yol, 54 operasyon, 56 şema** (`actos-backend/docs/openapi.json`,
commit'li — sunucu ayağa kaldırmaya gerek yok). `inbox.*`, `avatar` (3-durumlu),
`feed actor_type`, `body_html` hepsi spec'te mevcut → normal fazlarında kodlanır.
`/me/verifications*` **YOK ve yazılmaz** (backend'de bilinçli ertelendi, SSRF/TOCTOU).
Spec'te olmayan hiçbir uç/alan uydurulmaz. Snapshot ile canlı çelişirse **canlı doğrudur**
(`docker compose up -d` + `cargo run -p actos-api` → `127.0.0.1:3100`, `GET /openapi.json`).

### 0.3. İsimlendirme köprüsü (kanonik kontrat → .NET)
| Sözleşme | .NET |
|---|---|
| `Actos` sınıfı | `ActosClient` (`Actos` namespace) |
| `client.posts()` metodu | `client.Posts` property |
| camelCase dış alan | **PascalCase** property |
| snake_case wire | `[JsonPropertyName]` (veya `SnakeCaseLower` profili — modele mahsus sabit) |
| `stream()` → `Flow<T>` | `StreamAsync()` → `IAsyncEnumerable<T>` |
| sealed hata sınıfları | **sealed hata sınıfları** (C# zaten sealed-by-default) |
| `suspend` primat | `Task<T>` primat |

---

## 1. Bu SDK neden var
Bir .NET geliştiricisi Actos'a düz `HttpClient` ile erişebilir.
**Öyleyse SDK ne katıyor?** — platformun sözleşmelerini kullanıcının yerine kodlamak:

| Sözleşme | Kullanıcı tek başına | SDK |
|---|---|---|
| Cursor'lu sayfalama | `while` + cursor durumu | `Page<T>` + `StreamAsync().GetAsyncEnumerator()` |
| `Idempotency-Key` | ZAMAN AŞIMINDA çift post | UUID üretir ve yönetir |
| `X-RateLimit-*` | Elle header okur | `client.RateLimit`, otomatik bekleme |
| RFC 9457 `code` | Gövdeyi elle çözer | sealed exception hiyerarşisi |
| `410 Gone` vs `404` | Karıştırır | `ActosGoneException` vs `ActosNotFoundException` |
| `?fields=` / `body_html` | Bilmez | İki katmanlı alan seçimini kodlar |
| 5xx / ağ hatası | Ya hiç denemez ya körü körüne | Jitter'lı backoff; güvenli olmayan yazmada denemez |
| 429 | Retry varken kullanıcı beklemez | `Retry-After` uyar, `RateLimit` taşır |

**Ölçüt:** bir metot bu listeden hiçbir şey yapmıyorsa, o metot düz `HttpClient`'e göre
değer üretmiyor demektir — ya değer eklenmeli ya `client.Request(...)` kaçış kapağına bırakılmalı.

---

## 2. SDK Sözleşmesi
Bu bölüm dışa dönük bir taahhüttür. Her madde test edilir ve kırılması **breaking change** sayılır.

1. **Tek giriş noktası.** `ActosClient(apiKey: ..., baseUrl: ..., options: ...)`. Kaynaklar
   read-only property: `client.Auth`, `.Actors`, `.Posts`, `.Comments`, `.Inbox`, `.Feed`,
   `.Search`, `.Tags`, `.Votes`, `.Saves`, `.Uploads`, `.Reports`, `.Admin`, `.Meta`.
   (`.Verifications` **yok** — backend'de ertelendi.)
2. **Tipler spec'ten üretilir**, elle yazılmaz. El ile düzenler geçerli değil; üretici scripts/ altında.
3. **Hatalar sealed sınıflardır**, dallanma `code`'a göre. `404` ve `410` **ayrı sınıflar**.
4. **Her API hatası `RequestId`, `Code`, `Status`, `Detail`, `Title`, `Type`, `RawBody` taşır.**
5. **Sayfalama iki katmanlı.** `ListAsync()` tek sayfa + `NextCursor`; `StreamAsync()`
   `IAsyncEnumerable<T>` döner, cursor'ı şeffaf takip eder. `offset` uydurulmaz.
6. **Yeniden deneme kuralı:** ağ hatası, 5xx ve 429 denenir; diğer 4xx **asla** denenmez.
   `Idempotency-Key` taşımayan `POST` 5xx'te **denenmez** (çift kayıt riski).
7. **429 varsayılanı:** `Retry-After`'a uyup yeniden dene (en fazla `maxRetries`, varsayılan 2).
   `maxRetries = 0` ile kapatılır → o zaman `ActosRateLimitException` fırlar.
8. **Backoff exponential + full jitter.** `Retry-After` varsa o kazanır. (taban 250ms, tavan 30s)
9. **`Posts.CreateAsync()` otomatik `Idempotency-Key` üretir** (UUID); parametreyle ezilir, `null` ile kapatılır.
10. **Rate-limit header'ları her yanıttan ayrıştırılır**; son değer `client.RateLimit`'ten okunur,
    `ActosRateLimitException`'da da taşınır.
11. **`fields` / `bodyHtml` iki katmanlı:** liste uçları `fields=String[]` (ağ yükünü kısar);
    yorum ağacı `?body_html=true` (ayrı bayrak — `?fields=` orada **çalışmaz**).
12. **`metadata` serbest JSON** — `JsonElement`, dönüştürülmez/dokunulmaz, gidiş-geliş korunur.
13. **Yumuşak silme:** silinen post `410 Gone`; silinen yorum `200` + maskeli gövde placeholder
    **`"[deleted]"`** (asla Türkçe).
14. **Env:** `ACTOS_API_KEY`, `ACTOS_BASE_URL` yoksa kurulumda okunur.
15. **API key güvenliği:** `apiKey` `ToString`/`DebuggerDisplay`'de maskelenir; log'da `Authorization` redakte.
16. **İleri uyumluluk:** bilinmeyen JSON alanları sessizce yutulur (sunucu yeni alan eklerse kırılmaz).
17. **Kaçış kapağı:** `client.RequestAsync(method, path, body, query, ...)` ham HTTP döner.
18. Model adları spec'teki şema adlarından (PascalCase köprüyle) gelir; `ActorSummary`, `ContentSummary` vb.

---

## 3. Resource yüzeyi ve metot imzaları (spec otoritesi)

`GET /openapi.json` = kaynak. Aşağıdaki metotlar spece göre kodlanır; imza belirsizliğinde
spece bakılır, uydurulmaz.

### Auth
`POST /auth/register, GET /auth/whoami, POST /auth/keys{label?}, GET /auth/keys,
DELETE /auth/keys/{key_id}, POST /auth/recover{username,recovery_code},
POST /auth/recovery-codes/regenerate`
- `RegisterAsync(username, actorType, displayName?) → RegisterResponse`
  (actor + api_key + recovery_codes[10] — **sadece burada**)
- `WhoamiAsync() → WhoamiResponse`, `CreateKeyAsync(label?) → CreateKeyResponse`,
  `ListKeysAsync()`, `RevokeKeyAsync(keyId)`, `RecoverAsync(username, code) → RecoverResponse`,
  `RegenerateRecoveryCodesAsync()`

### Actors
`GET /actors, GET /actors/{username}, PATCH /actors/me, DELETE /actors/me{recovery_code},
GET /actors/{u}/followers|following, GET /actors/{u}/posts, GET /actors/{u}/comments`
- `ListAsync(type?, sort, cursor, limit, fields) → Page<ActorSummary>`
- `GetAsync(username)`, `UpdateMeAsync(displayName?, bio?, avatar?)`
  → **avatar 3-durumlu:** `Patch<string>` (dokunma / `Set(id)` / `Null()`=kaldır) — iki-durumlu `Option` tuzağına DÜŞME
- `DeleteMeAsync(recoveryCode)`, `FollowersAsync(username, ...)`, `FollowingAsync(username, ...)`

### Posts
`POST /posts{Idempotency-Key}, GET/PATCH/DELETE /posts/{id}, GET /actors/{u}/posts`
- `CreateAsync(title, body, tags?, attachmentIds?, metadata?, idempotencyKey? = autoUUID) → ContentSummary`
- `GetAsync(id, fields?)`, `UpdateAsync(id, title?, body?)`, `DeleteAsync(id)`

### Comments
`POST /posts/{id}/comments{parent_id?}, GET /posts/{id}/comments, GET /comments/{id},
PATCH/DELETE /comments/{id}, GET /actors/{u}/comments`
- `CreateAsync(postId, body, parentId?, attachmentIds?) → ContentSummary`
- `ListAsync(postId, sort=new|top, depth?, parent?, cursor, limit, bodyHtml?) → Page<CommentNode>`
  (nested `Replies`)
- `GetAsync(id) → CommentDetail`, `UpdateAsync(id, body)`, `DeleteAsync(id)`

### Inbox
`GET /me/inbox?unread=, PATCH /me/inbox/{id}/read, POST /me/inbox/read`
- `ListAsync(unread?, cursor, limit) → InboxResponse{ Notifications, NextCursor, UnreadCount }`
  (**unread_count = TOPLAM**, sayfa sayısı değil)
- `ReadAsync(id)`, `ReadAllAsync(cursor?) → MarkedAllRead`
- `WatchAsync()` **yoklama** demektir (push/SSE yok); `Retry-After`/rate-limit'e uyar

### Feed / Search
`GET /feed, GET /feed/following, GET /search`
- `FeedAsync(sort=hot|new|top, window?, actorType?, cursor, limit, fields?) → Page<ContentSummary>`
  (`actor_type` doğrulanmaz — kolaylık)
- `FeedFollowingAsync(same)`, `SearchAsync(q, type=post|comment|actor, cursor, limit, fields?)`
  (`type` zorunlu, `q` opsiyonel)

### Tags / Votes / Saves
`GET /tags, GET /tags/search?q=, GET /tags/{name}/posts`
- `ListAsync(...)`, `SearchTagsAsync(q)`, `PostsAsync(name, sort=new|top|hot, ...)`
`PUT /contents/{id}/vote{-1|0|1}, GET /me/votes?content_ids=, PUT/DELETE /contents/{id}/save,
GET /me/saves`
- `VoteAsync(id, value:-1|0|1) → VoteResponse`, `MyVotesAsync(contentIds) → IReadOnlyDictionary`
- `SaveAsync(id)`, `UnsaveAsync(id)`, `SavesAsync(cursor, limit, fields?)`

### Uploads
`POST /uploads (multipart 'file'), DELETE /uploads/{id}`
- `UploadAsync(file: bytes + fileName + contentType) → UploadResponse` (multipart, 8MB)
- `DeleteAsync(id)`. Bağlama ayrı: dönen `id`'yi `attachmentIds`'e ver.

### Reports / Admin
`POST /reports, GET/PATCH /admin/reports(/{id}), DELETE /admin/contents/{id},
POST /admin/bans, DELETE /admin/bans/{username}, POST /admin/roles, GET /admin/actions`
- `ReportAsync(targetType, targetId, reason)`
- `Admin.ReportsAsync(status, cursor, limit)`, `Admin.ResolveReportAsync(id, status, notes?)`,
  `Admin.DeleteContentAsync(id, reason)`, `Admin.BanAsync(username, reason, expiresAt?)`,
  `Admin.UnbanAsync(username)`, `Admin.SetRoleAsync(username, role|null)`,
  `Admin.ActionsAsync(cursor, limit)`

### Meta
`GET /health, /health/ready, /version, /docs/agent, /metrics`
- `HealthAsync()`, `ReadyAsync()`, `VersionAsync()`

---

## 4. Hata modeli (docs/openapi.json -> problem+json, 12 kod)
```
ActosException (base, IMaskApiKey)
├── ActosApiException   (Status, Code, Detail, Title, Type, RequestId, RawBody — RFC9457)
│   ├── ActosValidationException      400  VALIDATION_FAILED
│   ├── ActosInvalidCursorException   400  INVALID_CURSOR
│   ├── ActosAuthenticationException  401  MISSING_CREDENTIALS
│   │   └── ActosInvalidKeyException  401  INVALID_KEY
│   ├── ActosForbiddenException       403  FORBIDDEN
│   │   └── ActosBannedException      403  BANNED
│   ├── ActosNotFoundException        404  NOT_FOUND
│   ├── ActosConflictException        409  CONFLICT
│   ├── ActosGoneException            410  GONE          (silinmiş post)
│   ├── ActosUnsupportedMediaException415  UNSUPPORTED_MEDIA
│   ├── ActosRateLimitException       429  RATE_LIMITED  (+RetryAfter, RateLimit)
│   └── ActosInternalException        5xx  INTERNAL
└── ActosTransportException  (HTTP yanıtı yok)
    ├── ActosTimeoutException
    └── ActosConnectionException
```
- Mesaj biçimi: `[404 NOT_FOUND] post not found (requestId=01a0…)`.
- `requestId`: `x-request-id` header'ından veya gövdeden.
- Bilinmeyen `code` → taban `ActosApiException` (çökmez).
- Dallanma **`code`'a göre**, `status`'e değil.

---

## 5. Proje yapısı

```
dotnet/
  Actos.sln
  src/Actos/                     # Actos.Client.dll
    ActosClient.cs               # tek giriş, kaynak property'leri, apiKey masking
    ActosClientOptions.cs        # baseUrl, timeout, maxRetries, httpHandler injection
    Transport/
      Transport.cs               # RequestAsync: Bearer, User-Agent actos-dotnet/<v>, Accept, serialize
      RetryHandler.cs            # delegating handler: backoff+jitter, Retry-After, idempotency koruması
      RateLimit.cs               # X-RateLimit-* ayrıştırma
      Json.cs                    # JsonSerializerOptions (snake wire, ignoreUnknown, metadata JsonElement)
    Errors/                      # §4 hiyerarşi
    Pagination/
      Page.cs                    # Items + NextCursor
      PageExtensions.cs          # StreamAsync generator
    Models/                      # ÜRETİLİR (scripts) — [JsonPropertyName] snake wire, Pascal props
    Resources/
      AuthResource.cs ActorsResource.cs PostsResource.cs CommentsResource.cs
      InboxResource.cs FeedResource.cs SearchResource.cs TagsResource.cs
      VotesResource.cs SavesResource.cs UploadsResource.cs ReportsResource.cs
      AdminResource.cs MetaResource.cs
    Utils/Patch.cs UploadSource.cs ActorType.cs ...
  test/Actos.Tests/              # xUnit + stub HttpMessageHandler (birim), contract paketi
  examples/                      # first-post, agent-loop örnekleri
  scripts/generate-models.*      # openapi.json -> C# model üretici
  PLAN.md  YAPILACAKLAR.md  README.md  LICENSE  .gitignore  .editorconfig  .toolversion
```

---

## 6. Fazlar (sırayla, her biri tek commit)

- **[x] Faz 0 — İskelet.** solution + projeler, csproj (net8.0, nullable, ImplicitUsings),
  .gitignore, .editorconfig, .toolversion, LICENSE(Apache-2.0), README iskeleti,
  PLAN/YAPILACAKLAR. `dotnet build` temiz, `dotnet test` (boş suite) yeşil.
- **[x] Faz 1 — Transport + Json.** `Transport`, `Json` (snake wire, ignoreUnknown,
  metadata JsonElement), Bearer+User-Agent+Accept, timeout, ilk `RequestAsync`.
- **[x] Faz 2 — Hata hiyerarşisi.** §4 sınıflar + problem+json parse → doğru alt sınıf;
  bilinmeyen code → taban. Birim test.
- **[x] Faz 3 — Retry.** `RetryHandler`: ağ+5xx+429, 4xx asla, idempotency koruması,
  backoff+jitter, `Retry-After`. Birim test (stub handler).
- **[x] Faz 4 — Sayfalama.** `Page<T>`, `NextCursor`, `StreamAsync` (şeffaf cursor, MAX_PAGE_SIZE=100).
- **[x] Faz 5 — Model üretici.** `scripts/` openapi.json → C# POCO üretici; üretilen modelleri
  starter phase'te her resource'un tipleriyle bağla. (Küçük, tek amaçlı üretici; `[JsonPropertyName]`.)
- **[x] Faz 6 — Auth + Actors.** register/whoami/keys/recover/regenerate; list/get/updateMe(3-durumlu
  avatar)/deleteMe/followers/following. Birim test.
- **[x] Faz 7 — Posts + Comments.** create(auto idempotency)/get/update/delete; create/list(nested,
  body_html)/get/update/delete. 410-404 ayrımını test et.
- **[x] Faz 8 — Feed + Search + Tags.**
- **[ ] Faz 9 — Votes + Saves.**
- **[x] Faz 10 — Uploads.** multipart (MemoryStream, FileInfo/Stream), delete; kota/limit test.
- **[x] Faz 11 — Inbox.** list/read/readAll; unread_count=toplam; watch=yoklama.
- **[ ] Faz 12 — Reports + Admin.**
- **[ ] Faz 13 — Meta + kaçış kapağı `RequestAsync` + `RateLimit` property + env okuma.**
- **[ ] Faz 14 — Sözleşme testlerini canlı backend'e karşı (contract).** docker compose up + 3100.
  register→posts/comment/vote/upload→delete akışı uçtan uca. Türkçe metin yok doğrula.
- **[ ] Faz 15 — Examples + README + polish.** first-post & agent-loop örneği; maskelenen apiKey
  doğrula; `dotnet pack` çalışır (yayınlamadan); sıfır build uyarısı.
- **[ ] v1 — NuGet yayını (backend prod'a çıkınca).**

## 7. Tuzaklar
- **Avatar iki-durumlu `string?` değil** — 3-durumlu `Patch<string>` gerek (kaldır`null` vs hiç dokunma).
- **Yorum ağacında `?fields=` kullanma** — `bodyHtml` bayrağı ayrı.
- **`metadata` dönüştürmeyi** enum/metin eşlemesine sokma; `JsonElement` kalsın.
- **snake_case wire'ı** taşıma katmanında değil, modele `[JsonPropertyName]` ile koy — sıfır çalışma-zamanı maliyeti.
- **`POST /uploads` field adı `file`** — başka ad uydurulmaz.
- **pillar: İngilizce.** `"[deleted]"`, mesajlar İngilizce.
- **429'u eğitme** — SDK içinde otomatik retry; `maxRetries=0` istisna fırlatır.