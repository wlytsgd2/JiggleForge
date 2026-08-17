# Acknowledgements / 致谢

JiggleForge 的发展离不开先行研究、开源工具、测试反馈和社区支持。本文件不仅记录当前仓库的直接代码贡献，也记录曾为项目提供重要技术启发、研究基础、开发环境或验证帮助的人和项目。即使相关实现后来已经重新设计或完全重写，这些历史贡献仍然值得保留和感谢。

JiggleForge has benefited from prior research, open-source tools, testing, and community support. This document recognizes not only direct contributions to the current repository, but also people and projects that provided important technical inspiration, research foundations, development infrastructure, or validation. These historical contributions remain worthy of acknowledgement even when the related implementation has later been redesigned or completely rewritten.

## Prior Work and Inspiration / 先行工作与启发

### Rayvich / RZMenu

Rayvich 开发的 RZMenu，以及他对《绝区零》交互式模型变形技术的早期探索，是 JiggleForge 最初的技术参考和研究起点。RZMenu 最先让我认识到，这类实时交互可以在游戏中实现，并促使我开始研究如何将这一能力独立实现为可适配不同角色和 Mod 的通用工具。

JiggleForge 当前的运行时、着色器、桌面应用和 Mod 适配系统后来采用了独立设计与实现；这份实现独立性并不会抹去最初的启发和历史来源。感谢 Rayvich 在这一方向上的先行工作，以及后来围绕项目历史和兼容性进行的建设性交流。

Rayvich's RZMenu and his earlier exploration of interactive model deformation in *Zenless Zone Zero* served as the original technical reference and research starting point for JiggleForge. RZMenu first showed me that this kind of real-time interaction was possible and motivated the research into an independently implemented, general-purpose system for different characters and Mods.

The current JiggleForge runtime, shaders, desktop application, and Mod adaptation system were later independently designed and implemented. That implementation independence does not erase the original inspiration or historical provenance. Thank you to Rayvich for the pioneering work in this area and for the later constructive discussion about project history and compatibility.

## Tools and Infrastructure / 工具与基础设施

- **XXMI / ZZMI 与 3DMigoto** 提供 JiggleForge 所依赖的 Mod 加载和图形拦截环境。/ **XXMI / ZZMI and 3DMigoto** provide the Mod-loading and graphics-interception environment in which JiggleForge operates.
- **.NET 与 WinUI 3** 提供 JiggleForge Studio 使用的应用平台。/ **.NET and WinUI 3** provide the application platform used by JiggleForge Studio.
- **OpenAI Codex / GPT** 在项目作者的指导和审查下协助实现、分析、测试、文档编写与重构。/ **OpenAI Codex / GPT** assisted with implementation, analysis, testing, documentation, and refactoring under the project author's direction and review.

## Testing and Community / 测试与社区

感谢所有测试不同角色、Mod、场景、分辨率和硬件环境，并提供 FrameAnalysis、日志、复现步骤、翻译、建议和错误报告的用户。很多兼容性问题只有依靠这些实际环境中的反馈才能被发现和解决。

Thanks to everyone who tested different characters, Mods, scenes, resolutions, and hardware configurations, and who provided FrameAnalysis captures, logs, reproduction steps, translations, suggestions, and bug reports. Many compatibility issues could only be discovered and resolved through feedback from real user environments.

## Scope of Acknowledgement / 致谢范围

致谢表示某项工作对 JiggleForge 的历史、研究或开发产生了实际帮助，不自动表示该人员是当前代码的作者、版权所有者或项目维护者，也不表示双方存在正式隶属关系。当前仓库的直接代码贡献以 Git 历史和相应提交记录为准；实际分发的第三方代码或资源及其许可义务另见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

Acknowledgement means that a work materially helped the history, research, or development of JiggleForge. It does not automatically identify a person as an author, copyright holder, or maintainer of the current code, nor does it imply a formal affiliation. Direct contributions to this repository are recorded by its Git history and commits. Third-party code or assets actually distributed by the project, together with their licensing obligations, are documented separately in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
