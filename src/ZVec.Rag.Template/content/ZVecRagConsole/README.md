# ZVec RAG Console (TEMPLATE_LLM / TEMPLATE_EMBEDDER / TEMPLATE_STORAGE)

Generated app uses `FakeChatClient` and `DeterministicEmbedder` for a zero-dependency local run.

<!--#if (llm == "ollama")-->
Replace `FakeChatClient` with your Ollama `IChatClient` (Story 4.1 recipe packages).
<!--#endif-->
<!--#if (llm == "azure")-->
Replace `FakeChatClient` with Azure OpenAI `IChatClient`.
<!--#endif-->
<!--#if (llm == "openai")-->
Replace `FakeChatClient` with OpenAI `IChatClient`.
<!--#endif-->
<!--#if (llm == "llamasharp")-->
Replace `FakeChatClient` with LLamaSharp `IChatClient` when `ZVEC_LLAMA_MODEL` is set (H-LS-WRAP).
<!--#endif-->
<!--#if (llm == "fake")-->
Replace `FakeChatClient` with your `IChatClient` when you are ready for a real LLM.
<!--#endif-->

<!-- H-ONNX-TPL: Story 4.1.2 adds --embedder onnx -->

```bash
dotnet run
```
