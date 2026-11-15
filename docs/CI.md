# CI and tag releases

The [workflow](../.github/workflows/release.yml) builds on GitHub-hosted Windows 2022 runners for main pushes, pull requests and `v*` tags. Only a successful **tag push** publishes a Release. There is no manual Release step or `workflow_dispatch` publishing path. Release publication runs on a separate GitHub-hosted Ubuntu runner with `contents: write`; the build job has read-only repository access.

## Public dependencies

No local game upload, private dependency repository, Steam credentials or self-hosted runner is required. [Prepare-Dependencies.ps1](../ci/Prepare-Dependencies.ps1) downloads fixed files and verifies their SHA-256 hashes before use:

| Dependency | Source |
| --- | --- |
| Terraria server 1.4.5.8 | [Official dedicated-server ZIP](https://terraria.org/api/download/pc-dedicated-server/terraria-server-1458.zip) |
| Core 0.4.2, Harmony and Mono.Cecil | [Official Core release](https://github.com/Inidar1/terraria-modder/releases/tag/core-v0.4.2) |
| XNA Framework 4.0 | [Microsoft redistributable](https://www.microsoft.com/en-us/download/details.aspx?id=20914) |

XNA is installed only on the disposable CI machine when missing. Downloaded dependencies stay under the ignored `.ci-deps/` directory and never enter installation/source archives or uploaded artifacts.

The public Windows server includes the shared shop/crafting methods needed for headless tests, but its assembly identity is `TerrariaServer` and Main inherits `Terraria.Server.Game`. CI uses Mono.Cecil to create a temporary client-compatible reference/test assembly: identity `Terraria`, supported version 1.4.5.8, Main base `Microsoft.Xna.Framework.Game`. The source files and hashes are pinned; unexpected upstream identities/base types fail preparation. No game/server entry point is launched.

The built mod is audited for the client assembly identity and absence of `TerrariaServer` or `Terraria.Server.*` references. For the 1.0 release, the same test executable compiled against this public reference also passed all 272 assertions when run against the actual local Terraria client. These headless checks do not replace graphical in-game verification, especially after changes to the reference adapter or game APIs.

## Checks and assets

1. Validate `vMAJOR.MINOR.PATCH` against manifest and both assembly version attributes. Release tags must point to a commit on main.
2. Prepare verified public dependencies and run the real native/config/quantity/lifecycle/logging assertions.
3. Exercise real packaging, including stale files and junction cleanup refusal.
4. Reject malformed version tags, corrupted checksums and unexpected runtime/source archive contents.
5. Audit client references and verify exactly three runtime files: DLL, manifest and LICENSE. Verify source documentation and exclude game/dependency binaries.
6. Upload only the two mod archives and `SHA256SUMS.txt` as CI artifacts.
7. On a tag push, download that build job's exact artifact ID, check hashes again, then let `gh release create --verify-tag` publish the three files.

Existing Releases are not silently overwritten. A failed build cannot enter the publication job. Release notes include the build run URL and commit SHA.

## Publishing an approved version

Update manifest and assembly versions together, add `docs/releases/<version>.md`, and push the reviewed commit to main. Let branch CI pass before pushing an annotated tag:

```powershell
git tag -a v1.0.0 -m 'Shift Batch 1.0.0'
git push origin v1.0.0
```

The tag workflow performs the build again and creates the Release itself. Do not upload locally built ZIPs to replace its assets. Download the published ZIP and `SHA256SUMS.txt`, run `ci/Verify-Package.ps1` against the downloaded files, then install from that downloaded archive after closing the game. Preserve the Core configuration file.

## 简体中文

所有构建均在 GitHub 云端执行，依赖从官方公开下载地址获取并校验固定 SHA-256，无需本机 runner、Steam 登录或私有依赖仓库。官方服务端仅在 CI 中临时适配为客户端 API 编译／无界面测试引用，不启动游戏入口、不进入发包附件；编译出的 mod 会检查客户端程序集引用。

普通 main／PR 构建只做检测和保存候选包；推送与 manifest、DLL 版本一致的 `v*` tag 后，测试全部通过才自动创建 Release，附件为安装 ZIP、源码 ZIP、SHA256SUMS.txt。正式验证应下载该 Release 的安装包、核对校验和，关闭游戏后再替换本地 mod，并保留用户配置。
