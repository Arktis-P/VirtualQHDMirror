# VirtualQHDMirror

**言語:** [English](README.md) · [한국어](README.ko.md) · **日本語**

VirtualQHDMirrorは、Windowsの**仮想QHD（2560×1440）デスクトップを物理FHD（1920×1080）ディスプレイにミラー表示**し、マウス操作を仮想画面へ転送するアプリケーションです。

## ダウンロード · [v0.22リリースノート](RELEASE_NOTES_v0.22.ja.md)

**対応環境:** Windows 11 x64

[GitHub Releases](https://github.com/Arktis-P/VirtualQHDMirror/releases)で**Setup.exe（インストール版）**と**ZIP（ポータブル版）**を提供します。インストーラーは現在のユーザーにアプリだけを導入し、仮想ディスプレイドライバーはインストールしません。ZIP版は展開後に`VirtualQHDMirror.exe`を実行し、SessionとElevatedHelperのEXEも同じフォルダーに保持してください。

.NET 8ランタイムを内包しているため、別途ランタイムをインストールする必要はありません。**アプリとインストーラーは未署名**のため、Windowsで発行元に関する警告が表示される場合があります。

**仮想ディスプレイドライバーは同梱されません。** **Get driver**ボタンは外部の[ge9/IddSampleDriver公式Releases](https://github.com/ge9/IddSampleDriver/releases)を開きます。導入方法・署名・セキュリティ上の注意点は配布元のドキュメントを確認してください。VQMはドライバーの自動インストール、証明書の登録、Secure Bootの無効化、テスト署名モードの有効化を行いません。

## 初期設定

1. アプリを起動し**Virtual Display Setup**で仮想ディスプレイの状態を確認します。
2. 仮想QHDがない場合は**Get driver**で外部配布ページの説明を確認します。
3. 互換ドライバーで**2560×1440**の仮想モニターを設定します。ge9固有の設定は開発元の説明を参照してください。
4. VQMに戻り**Recheck**を押して仮想QHDソースと物理FHD出力先を確認します。
5. ソースと出力先を選び**Start Mirror**を押します。ドライバー設定変更前にはミラーリングを停止してください。

## 主な機能

- QueryDisplayConfigによる物理・仮想ディスプレイの検出
- Virtual Display Setup、Get driver、Recheck
- D3D11 Desktop Duplicationと出力先VSyncに基づくミラー表示
- Portal方式のマウス入力、論理カーソル制御、ディスプレイ間ドラッグ
- Windowsログイン時の自動起動オプション
- GDI描画とv0.14.1レガシーフォールバック

## ソースからビルド

Windowsに**.NET 8 SDK**をインストールし、リポジトリのルートで実行します。

```powershell
.\build-v022.cmd
.\run-v022.cmd
```

`artifacts/release/v0.22/win-x64/`に実行ファイルが生成されます。`VirtualQHDMirror.exe`、`VirtualQHDMirror.Session.exe`、`VirtualQHDMirror.ElevatedHelper.exe`は同じフォルダーに置いてください。ZIPとSHA-256チェックサムも生成されます。

**Inno Setup 6**を導入すると`build-v022-installer.cmd`でSetup.exeをビルドできます。`artifacts/`はGitの管理対象外です。既存の`build-release.cmd`と`run.cmd`はv0.21.2フォールバック用です。

## 制限事項と関連資料

互換性のある仮想QHDドライバーが別途必要です。現在のミラーリングは仮想ソースと物理出力先を1組選びます。一部ドライバー設定では管理者の承認が必要です。リポジトリのVQM独自IDDは**未署名の実験用ソース**で、配布アプリには含まれません。

[English notes](RELEASE_NOTES_v0.22.md) · [한국어 출시노트](RELEASE_NOTES_v0.22.ko.md) · [日本語リリースノート](RELEASE_NOTES_v0.22.ja.md)

[配布手順](docs/V022_RELEASE_PACKAGING.md) · [外部ドライバー方針](docs/V022_STAGE_B_EXTERNAL_DRIVER_PIVOT.md) · [第三者ライセンス](THIRD_PARTY_NOTICES.md)

旧バージョンは`archive/`に保管し、明示的な承認なくGitHub Actionsは使用しません。
