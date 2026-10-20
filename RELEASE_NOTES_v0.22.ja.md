# VirtualQHDMirror v0.22

**言語:** [English](RELEASE_NOTES_v0.22.md) · [한국어](RELEASE_NOTES_v0.22.ko.md) · **日本語**

Windows 11 x64 — 外部仮想ディスプレイドライバーのセットアップ案内を追加しました。

## 主な変更点

- **Virtual Display Setup:** 未検出/準備完了/QHDモード/再起動の状態を表示
- **Get driver:** [ge9/IddSampleDriver公式Releases](https://github.com/ge9/IddSampleDriver/releases)へのリンクとサードパーティ製ドライバーの注意事項
- **Recheck:** ドライバー導入後のディスプレイ状態を再確認
- v0.21.2のD3D11 + Portalミラーリング、マウス・ドラッグ連携、Windowsログイン時の自動起動を維持

## ダウンロード

- `VirtualQHDMirror-v0.22-win-x64-Setup.exe`: ユーザー単位のアプリインストーラー
- `VirtualQHDMirror-v0.22-win-x64.zip`: ポータブル版
- 各ファイルのSHA-256チェックサム

**仮想ディスプレイドライバーは同梱・自動インストールされません。** ge9は外部ソフトウェアです。アプリ/インストーラーは未署名で、Windowsの発行元警告が出る場合があります。Secure Bootや証明書の信頼設定は変更しません。

## 検証状況

開発PCでアプリのビルド・起動、Get driver、Recheck/Ready、既存ミラーリングPASS。別PCでビルド・起動・Missing検出PASS。新しいSetup.exeのインストールとアンインストールは公開前の追加検証が必要です。

[日本語 README](README.ja.md)
