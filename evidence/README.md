# Evidence 目录

此目录存放每个功能的证据文件。

## 目录结构

每个功能一个子目录，命名规则：`<Sprint>-<TaskID>-<功能名>`

```
evidence/
├── S1-01-CoreInterfaces/
│   ├── PROOF.md           # 证据汇总（必须）
│   ├── ARCH_CHECK.md      # 架构一致性检查
│   └── test_output.txt    # 测试输出
├── S1-02-DoubleBuffer/
│   ├── PROOF.md
│   ├── stress_test.log    # 压测日志
│   └── ...
└── Sprint1/
    └── E2E_PROOF.md       # 端到端验收证据
```

## PROOF.md 必须包含

1. **需求引用** — 来自哪个 spec（章节/页码）
2. **实现范围** — 改了哪些文件
3. **可重复步骤** — 如何运行验证
4. **结果证据** — 粘贴测试输出/日志
5. **结论** — 通过/不通过

## 模板

见 `spec/DEV_PLAYBOOK.md` 第三部分
