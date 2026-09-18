# Changelog

All notable changes to `Actos.Client` are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to the
Actos backend version it targets.

## [0.3.0] — 2026-09-18

Synced to the Actos backend 0.3.0 spec (communities).

### Added

- `CommunitiesResource` (exposed as `ActosClient.Communities`): the community directory,
  create/edit, join/leave/kick, the community post feed, succession and closure, plus the
  private-community invitation and application flows. Includes `StreamAsync` for the directory
  and `StreamPostsAsync` for a community's posts.
- Generated models: `CommunitySummary`, `CommunityRefSummary`, `CommunityListResponse`,
  `CommunityMemberSummary`, `CommunityMemberListResponse`, `InvitationSummary`,
  `InvitationListResponse`, `ApplicationSummary`, `ApplicationListResponse`,
  `CrossPostPreviewSummary`, `CreateCommunityRequest`, `UpdateCommunityRequest`,
  `CreateInvitationRequest`, `CreateApplicationRequest`, `SuccessorRequest`,
  `PermissionSummary`, `SetPermissionRequest`.
- `Permission`, `CommunityVisibility` and `ApplicationStatus` well-known value constants.
- `ContentSummary`/`CommentNodeResponse` now carry `community`, `is_cross_post` and `cross_post`.
- `CreatePostRequest` gains `community` and `cross_post_source`; `PostsResource.CreateAsync`
  exposes both (`community`, `crossPostSource`).
- `CreateBanRequest` gains `community` and `delete_posts`; `AdminResource.BanAsync` exposes both
  and `AdminResource.UnbanAsync` gains a `community` query.

### Changed

- `WhoamiResponse.roles` (`IReadOnlyList<string>`) is replaced by `permissions`
  (`IReadOnlyList<PermissionSummary>`), matching the scoped-permission model.
- `BanSummary` and `ReportSummary` gain an optional `community`.

### Removed

- `AdminResource.SetRoleAsync` and `POST /admin/roles` (replaced by
  `AdminResource.GrantPermissionAsync` / `AdminResource.RevokePermissionAsync` on
  `PUT`/`DELETE /admin/permissions`).
- The `Role` constants class and the generated `SetRoleRequest` model.

[0.3.0]: https://github.com/actos-dev/dotnet/releases/tag/v0.3.0
