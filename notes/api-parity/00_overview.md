# API parity 索引

本目录保存 legacy 契约迁移、兼容策略和已完成对齐记录。这里不是当前唯一事实源；当前行为以 `roadmap/`、`review-issues/` 和代码测试为准。

## 阅读顺序

1. [01_contract_freeze.md](./01_contract_freeze.md)
   - 早期契约冻结记录，保留历史语义和字段来源。
2. [12_legacy_api_contract_extract.md](./12_legacy_api_contract_extract.md)
   - 当前更接近活动口径的 legacy API 提炼。
3. [11_contracts_json_mapping.md](./11_contracts_json_mapping.md)
   - JSON 命名和 DTO 映射进展。
4. [10_webhost_middleware_shell.md](./10_webhost_middleware_shell.md)
   - WebHost 基础中间件和会话兼容记录。
5. [02_legacy_data_model.md](./02_legacy_data_model.md) 到 [09_notice_core_merge.md](./09_notice_core_merge.md)
   - 存储、安全、notice 兼容过程记录。

## 文件清单

- [01_contract_freeze.md](./01_contract_freeze.md)：第一批契约冻结记录。
- [02_legacy_data_model.md](./02_legacy_data_model.md)：legacy data 目录模型冻结。
- [03_legacy_readonly_layer.md](./03_legacy_readonly_layer.md)：legacy 只读加载层记录。
- [04_secure_json_compat.md](./04_secure_json_compat.md)：secure-json 兼容进展。
- [05_notice_dpapi_compat.md](./05_notice_dpapi_compat.md)：公告缓存 DPAPI 兼容。
- [06_notice_signature_hash_chain.md](./06_notice_signature_hash_chain.md)：公告索引验签和哈希校验。
- [07_notice_feed_local_assembly.md](./07_notice_feed_local_assembly.md)：notice feed 本地组装。
- [08_notice_access_failclosed.md](./08_notice_access_failclosed.md)：notice access / fail-closed。
- [09_notice_core_merge.md](./09_notice_core_merge.md)：notice 核心合并语义。
- [10_webhost_middleware_shell.md](./10_webhost_middleware_shell.md)：WebHost 基础中间件。
- [11_contracts_json_mapping.md](./11_contracts_json_mapping.md)：Contracts JSON 映射。
- [12_legacy_api_contract_extract.md](./12_legacy_api_contract_extract.md)：legacy API 当前提炼。

## 状态说明

- `01-11` 多数是迁移过程记录，可能包含“当前仍缺”等历史措辞。
- 当前开发决策应优先看 `12_legacy_api_contract_extract.md`、`../roadmap/00_development_direction.md` 和 `../review-issues/00_current_issues.md`。
- 若 API 行为已经被代码和测试更新，应同步更新本目录或在相关文件顶部标明历史状态。
