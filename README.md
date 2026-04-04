# agent-framework-v1-lab

Microsoft Agent Framework (v1) の各機能を試すラボプロジェクトです。  
Azure OpenAI Service と `Microsoft.Agents.AI` を使い、さまざまなエージェントのパターンをシンプルなコードで紹介しています。

## 前提条件

- .NET 10 SDK
- Azure OpenAI Service のエンドポイントとデプロイ名
- Azure CLI (`az login` 済み) ※ `AzureCliCredential` を使用
- `az login` しているユーザーに Azure OpenAI リソースへの **Cognitive Services OpenAI User** ロールが必要です

## 設定

各プロジェクトは [.NET User Secrets](https://learn.microsoft.com/ja-jp/aspnet/core/security/app-secrets) で接続情報を管理しています。  
全プロジェクトが同じ `UserSecretsId` (`agent-framework-v1-lab`) を共有しているため、以下のコマンドは **1回だけ** 実行すれば OK です。

```bash
dotnet user-secrets set --id agent-framework-v1-lab "Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set --id agent-framework-v1-lab "DeploymentName" "<your-deployment-name>"
```

## プロジェクト一覧

| プロジェクト | 概要 |
|---|---|
| [HelloAgent](./HelloAgent/) | 最もシンプルなエージェント。非ストリーミング・ストリーミング両方の呼び出しを確認できます。 |
| [MultiTurnAgent](./MultiTurnAgent/) | `AgentSession` を使ってマルチターン会話のコンテキストを保持するサンプルです。 |
| [AgentWithTools](./AgentWithTools/) | `AIFunctionFactory` でツール（Function calling）を登録するサンプルです。 |
| [ContextProviderAgent](./ContextProviderAgent/) | `AIContextProvider` を使ってエージェント実行前にコンテキスト情報（例: スケジュール）を動的に注入するサンプルです。 |
| [AgentRunMiddleware](./AgentRunMiddleware/) | エージェントの実行パイプラインにミドルウェアを追加し、ロギングやガードレール処理を行うサンプルです。 |
| [FunctionCallMiddleware](./FunctionCallMiddleware/) | Function calling のミドルウェアを使い、ツール呼び出しの前後に処理を挿入するサンプルです。 |
| [MemoryAgent](./MemoryAgent/) | `AIContextProvider` と `ProviderSessionState` を組み合わせて、セッションをまたいでユーザー情報を記憶するサンプルです。 |

## ライセンス

[MIT](./LICENSE)
