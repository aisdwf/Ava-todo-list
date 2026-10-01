using Avalonia.Controls;
using Avalonia.Media;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Views.Guide;

/// <summary>
/// 操作指南与引导气泡中的 11 个矢量演示场景（spec-onboarding-guide，R-6.2）。
/// </summary>
/// <remarks>
/// 每个场景是 FlowTask 界面的简化线框：同一套令牌、同一描边图标，
/// 用户看到的形状就是真实界面里要找的形状。演示脚本逐步改属性，由 Transitions 补间。
/// </remarks>
internal static class GuideScenes
{
    /// <summary>场景设计宽度；舞台按比例缩放。</summary>
    public const double StageWidth = 320;

    /// <summary>场景设计高度。</summary>
    public const double StageHeight = 180;

    /// <summary>构建场景并返回其循环脚本。</summary>
    public static SceneScript Build(GuideSceneKind kind, SceneLayer layer) => kind switch
    {
        GuideSceneKind.AddTask => AddTask(layer),
        GuideSceneKind.DueAndPriority => DueAndPriority(layer),
        GuideSceneKind.EditAndComplete => EditAndComplete(layer),
        GuideSceneKind.DeleteTask => DeleteTask(layer),
        GuideSceneKind.Projects => Projects(layer),
        GuideSceneKind.DeleteProject => DeleteProject(layer),
        GuideSceneKind.QuickCapture => QuickCapture(layer),
        GuideSceneKind.QuickWindowKeys => QuickWindowKeys(layer),
        GuideSceneKind.DefaultDue => DefaultDue(layer),
        GuideSceneKind.CloseToTray => CloseToTray(layer),
        GuideSceneKind.Theme => Theme(layer),
        _ => new SceneScript()
    };

    /// <summary>主窗右侧内容区的简化外框：Hero 标题 + 添加栏发丝线。</summary>
    private static (TextBlock Input, Border Caret) ContentFrame(SceneLayer layer, string watermark = "记录下一件重要的事…")
    {
        layer.Micro(12, 10, "所有任务的综合看板", "AccentBrush");
        layer.Text(12, 20, "全部任务", 17, "TextPrimaryBrush", FontWeight.Bold);
        layer.Box(12, 66, 296, 1, "HairlineStrongBrush", 0);
        var input = layer.Text(12, 48, watermark, 11, "TextTertiaryBrush");
        var caret = layer.Box(12, 48, 1.4, 13, "AccentBrush", 0.7);
        return (input, caret);
    }

    private static SceneScript AddTask(SceneLayer layer)
    {
        var (input, caret) = ContentFrame(layer);
        var enter = layer.Key(270, 45, "↵");
        var existing = layer.TaskRow(74, "整理本周周报");
        var added = layer.TaskRow(74, "给设计稿写评审意见");
        var cursor = layer.Cursor();

        const string title = "给设计稿写评审意见";
        var script = new SceneScript
        {
            Reset = () =>
            {
                input.Text = "记录下一件重要的事…";
                layer.Brushes.Set(input, TextBlock.ForegroundProperty, "TextTertiaryBrush");
                Motion.Place(caret, 0, 0);
                Motion.Hide(caret);
                layer.Press(enter, false);
                existing.Reset();
                added.Reset();
                Motion.Hide(added.Frame);
                Motion.Place(added.Frame, 0, -14);
                cursor.Park();
            }
        };

        script.MoveTo(cursor, 90, 55)
            .Click(cursor, 90, 55)
            .Then(260, () =>
            {
                input.Text = string.Empty;
                layer.Brushes.Set(input, TextBlock.ForegroundProperty, "TextPrimaryBrush");
                Motion.Show(caret);
                cursor.Park();
            });
        for (var i = 1; i <= title.Length; i++)
        {
            var partial = title[..i];
            var offset = i * 11.2;
            script.Then(120, () =>
            {
                input.Text = partial;
                Motion.Place(caret, offset, 0);
            });
        }

        return script.Wait(360)
            .Then(220, () => layer.Press(enter, true))
            .Then(60, () =>
            {
                layer.Press(enter, false);
                // 新任务「如呼吸般落入列表」：旧行下移让位，新行从输入栏位置淡入落下
                Motion.Place(existing.Frame, 0, 30);
                Motion.Show(added.Frame);
                Motion.Place(added.Frame, 0, 0);
                input.Text = "记录下一件重要的事…";
                layer.Brushes.Set(input, TextBlock.ForegroundProperty, "TextTertiaryBrush");
                Motion.Place(caret, 0, 0);
            })
            .Wait(2200);
    }

    /// <remarks>
    /// 与真实添加栏一致（spec-due-date-picker）：标题 | 「到期」入口 | P1–P3 同处一行；
    /// 点「到期」在其下方弹出日期框（预设 + 日历），点某一天即生效，入口随即显示相对日期。
    /// 不演示数字输入：用户 2026-09-29 裁决删除。
    /// </remarks>
    private static SceneScript DueAndPriority(SceneLayer layer)
    {
        layer.Text(12, 14, "买机票", 13, "TextPrimaryBrush", FontWeight.SemiBold);
        var dueChip = layer.Pill(144, 14, 62, "到期", null, "TextTertiaryBrush");
        var p1 = layer.Pill(212, 14, 30, "P1", null, "TextTertiaryBrush");
        var p2 = layer.Pill(244, 14, 30, "P2", "AccentSubtleBrush", "AccentBrush");
        var p3 = layer.Pill(276, 14, 30, "P3", null, "TextTertiaryBrush");
        layer.Box(12, 40, 296, 1, "HairlineStrongBrush", 0);

        // 下方的任务列表：日期框叠在它上面，不把它推下去
        layer.TaskRow(52, "写周报");
        layer.TaskRow(80, "续签合同");

        // 贴边日期框：右对齐到「到期」入口，向左展开
        const double popX = 86;
        const double popY = 44;
        var (popover, pop) = layer.Group(popX, popY, 120, 126, "CardSurfaceBrush", 8, "HairlineStrongBrush");
        var presetDefault = pop.Pill(8, 8, 58, "默认 +1 天", "HairlineBrush", "TextSecondaryBrush");
        pop.Pill(70, 8, 36, "清除", "HairlineBrush", "TextSecondaryBrush");
        pop.Text(8, 30, "三月", 8.5, "TextSecondaryBrush", FontWeight.SemiBold);

        // 迷你日历：5 行 × 7 列，今天为 10 日；演示点选 12 日
        const double cellW = 14.5;
        const double cellH = 14;
        const double gridX = 6;
        const double gridY = 44;
        const int pickDay = 12;
        Border? picked = null;
        for (var day = 1; day <= 31; day++)
        {
            var index = day + 6 - 1; // 3 月 1 日排在第 7 列
            var col = index % 7;
            var row = index / 7;
            var cell = pop.Cell(
                gridX + (col * cellW),
                gridY + (row * cellH),
                cellW - 1.5,
                cellH - 1.5,
                day.ToString(System.Globalization.CultureInfo.InvariantCulture),
                day == 10 ? "HairlineStrongBrush" : null,
                "TextSecondaryBrush",
                3,
                7);
            if (day == pickDay)
            {
                picked = cell;
            }
        }

        var pickIndex = pickDay + 6 - 1;
        var pickX = popX + gridX + ((pickIndex % 7) * cellW) + (cellW / 2);
        var pickY = popY + gridY + ((pickIndex / 7) * cellH) + (cellH / 2);

        layer.Micro(12, 150, "点一天即生效，Esc 或点外面取消");
        var cursor = layer.Cursor();

        static void SetPillText(Border pill, string text)
        {
            if (pill.Child is TextBlock label)
            {
                label.Text = text;
            }
        }

        void ShowPopover(bool on)
        {
            if (on)
            {
                Motion.Show(popover);
                Motion.Place(popover, 0, 0);
            }
            else
            {
                Motion.Hide(popover);
                Motion.Place(popover, 0, -6);
            }
        }

        var script = new SceneScript
        {
            Reset = () =>
            {
                SetPillText(dueChip, "到期");
                layer.Tint(dueChip, null, "TextTertiaryBrush");
                layer.Tint(presetDefault, "HairlineBrush", "TextSecondaryBrush");
                if (picked is not null)
                {
                    layer.Tint(picked, null, "TextSecondaryBrush");
                }

                layer.Tint(p1, null, "TextTertiaryBrush");
                layer.Tint(p2, "AccentSubtleBrush", "AccentBrush");
                layer.Tint(p3, null, "TextTertiaryBrush");
                ShowPopover(false);
                cursor.Park();
            }
        };

        return script.MoveTo(cursor, 175, 24)
            .Click(cursor, 175, 24)
            .Then(500, () => ShowPopover(true))
            .MoveTo(cursor, pickX, pickY)
            .Then(300, () =>
            {
                if (picked is not null)
                {
                    layer.Tint(picked, "AccentSubtleBrush", "AccentBrush");
                }
            })
            .Click(cursor, pickX, pickY)
            .Then(500, () =>
            {
                // 点选即生效：日期框关闭，入口显示相对日期
                ShowPopover(false);
                SetPillText(dueChip, "2 天后");
                layer.Tint(dueChip, "AccentSubtleBrush", "AccentBrush");
            })
            .MoveTo(cursor, 227, 22)
            .Click(cursor, 227, 22)
            .Then(900, () =>
            {
                layer.Tint(p1, "PriorityHighSurfaceBrush", "PriorityHighBrush");
                layer.Tint(p2, null, "TextTertiaryBrush");
                cursor.Park();
            })
            .Wait(1600);
    }

    /// <remarks>
    /// 与真实任务行一致（spec-inline-task-edit）：勾选完成后沉底；双击标题原地变输入框，
    /// 打字后回车保存；点 P 标签在其下方弹出三档小框，点一档即生效。不再有底部编辑面板。
    /// </remarks>
    private static SceneScript EditAndComplete(SceneLayer layer)
    {
        var first = layer.TaskRow(12, "回复客户邮件");
        var second = layer.TaskRow(42, "准备周会材料");

        // 第二行标题上的就地输入框：与标题同位，双击后浮现
        var (input, inputLayer) = layer.Group(34, 44, 170, 24, "CardSurfaceBrush", 4, "AccentBrush");
        var inputText = inputLayer.Text(4, 5, "准备周会材料", 11, "TextPrimaryBrush", FontWeight.SemiBold);

        // 第二行右侧的 P 标签与其下方的三档小框
        const double tagX = 250;
        const double tagY = 48;
        var tag = layer.Pill(tagX, tagY, 26, "P2", "PriorityMediumSurfaceBrush", "PriorityMediumBrush");
        var (popover, pop) = layer.Group(tagX - 34, tagY + 22, 60, 62, "CardSurfaceBrush", 6, "HairlineStrongBrush");
        var optionHigh = pop.Pill(6, 6, 48, "P1", "PriorityHighSurfaceBrush", "PriorityHighBrush");
        pop.Pill(6, 24, 48, "P2", "PriorityMediumSurfaceBrush", "PriorityMediumBrush");
        pop.Pill(6, 42, 48, "P3", "PriorityLowSurfaceBrush", "PriorityLowBrush");
        var cursor = layer.Cursor();

        void ShowInput(bool on)
        {
            Motion.Show(input, on);
            second.Title.Opacity = on ? 0 : 1;
        }

        void ShowPopover(bool on)
        {
            Motion.Show(popover, on);
            Motion.Place(popover, 0, on ? 0 : -6);
        }

        var script = new SceneScript
        {
            Reset = () =>
            {
                first.Reset();
                second.Reset();
                second.Title.Text = "准备周会材料";
                inputText.Text = "准备周会材料";
                ShowInput(false);
                ShowPopover(false);
                Motion.Place(tag, 0, 0);
                SceneLayer.Relabel(tag, "P2");
                layer.Tint(tag, "PriorityMediumSurfaceBrush", "PriorityMediumBrush");
                layer.Tint(optionHigh, "PriorityHighSurfaceBrush", "PriorityHighBrush");
                cursor.Park();
            }
        };

        return script.MoveTo(cursor, 22, 26)
            .Then(240, () => layer.Brushes.Set(first.Ring, Avalonia.Controls.Shapes.Shape.StrokeProperty, "AccentBrush"))
            .Click(cursor, 22, 26)
            .Then(700, first.Complete)
            // 已完成任务沉到底部：两行交换位置，第二行的 P 标签随行上移
            .Then(900, () =>
            {
                Motion.Place(first.Frame, 0, 30);
                Motion.Place(second.Frame, 0, -30);
                Motion.Place(tag, 0, -30);
                Motion.Place(input, 0, -30);
                Motion.Place(popover, 0, -36);
            })
            // 双击标题：两次点击环
            .MoveTo(cursor, 90, 26)
            .Then(200, () => Motion.Show(second.Hover))
            .Click(cursor, 90, 26)
            .Click(cursor, 90, 26)
            .Then(400, () => ShowInput(true))
            .Type(inputText, "准备周会材料和议程", 140, "准备周会材料")
            // 回车保存：输入框收起，标题换成新文案
            .Then(700, () =>
            {
                second.Title.Text = "准备周会材料和议程";
                ShowInput(false);
            })
            // 点 P 标签，弹出三档小框，点 P1 即生效
            .MoveTo(cursor, tagX + 13, tagY - 22)
            .Click(cursor, tagX + 13, tagY - 22)
            .Then(500, () =>
            {
                Motion.Show(popover);
                Motion.Place(popover, 0, -30);
            })
            .MoveTo(cursor, tagX - 34 + 30, tagY + 22 + 14 - 30)
            .Then(250, () => layer.Tint(optionHigh, "PriorityHighBrush", "OnAccentBrush"))
            .Click(cursor, tagX - 34 + 30, tagY + 22 + 14 - 30)
            .Then(900, () =>
            {
                Motion.Hide(popover);
                SceneLayer.Relabel(tag, "P1");
                layer.Tint(tag, "PriorityHighSurfaceBrush", "PriorityHighBrush");
                cursor.Park();
            })
            .Wait(1600);
    }

    private static SceneScript DeleteTask(SceneLayer layer)
    {
        var first = layer.TaskRow(20, "取快递");
        var second = layer.TaskRow(50, "旧的会议提醒");
        var third = layer.TaskRow(80, "更新简历");
        var cursor = layer.Cursor();

        var script = new SceneScript
        {
            Reset = () =>
            {
                first.Reset();
                second.Reset();
                third.Reset();
                cursor.Park();
            }
        };

        return script.MoveTo(cursor, 150, 64)
            .Then(500, () =>
            {
                // 悬停整行才浮现操作按钮（design-visual-language §5.2）
                Motion.Show(second.Hover);
                Motion.Show(second.Remove);
            })
            .MoveTo(cursor, 302, 64)
            .Then(200, () => layer.Brushes.Set(second.Remove, Avalonia.Controls.Shapes.Shape.StrokeProperty, "PriorityHighBrush"))
            .Click(cursor, 302, 64)
            .Then(420, () =>
            {
                Motion.Hide(second.Frame);
                Motion.Place(second.Frame, 24, 0);
            })
            .Then(1600, () =>
            {
                Motion.Place(third.Frame, 0, -30);
                cursor.Park();
            });
    }

    /// <summary>侧栏线框：VIEWS、PROJECTS 与「新建项目」。</summary>
    private static SceneScript Projects(SceneLayer layer)
    {
        var (_, side) = layer.Group(10, 10, 132, 160, "SidebarWashBrush", 8);
        side.Micro(12, 10, "VIEWS");
        side.Cell(8, 22, 116, 20, "全部任务", null, "TextSecondaryBrush");
        side.Micro(12, 52, "PROJECTS");
        var work = side.Box(8, 64, 116, 20, null, 10);
        side.Dot(16, 71, 6, "PriorityMediumBrush");
        side.Text(28, 68, "工作", 9.5, "TextSecondaryBrush", FontWeight.Medium);

        var newRow = side.Box(8, 88, 116, 20, null, 10);
        var newDot = side.Dot(16, 95, 6, "PriorityLowBrush");
        var newName = side.Text(28, 92, "读书", 9.5, "TextSecondaryBrush", FontWeight.Medium);
        var renameBox = side.Box(24, 89, 96, 18, null, 4, "AccentBrush");

        var add = side.Box(8, 112, 116, 20, null, 10);
        side.Icon(16, 117, 9, "IconPlus", "TextTertiaryBrush");
        var addLabel = side.Text(30, 115, "新建项目", 9, "TextTertiaryBrush");
        var inputBox = side.Box(12, 112, 108, 20, null, 4, "AccentBrush");
        var inputText = side.Text(20, 116, string.Empty, 9.5);

        var title = layer.Text(158, 22, "全部任务", 17, "TextPrimaryBrush", FontWeight.Bold);
        var hint = layer.Micro(158, 12, "所有任务的综合看板", "AccentBrush");
        var rowA = layer.TaskRow(56, "季度计划", 150, 154);
        var rowB = layer.TaskRow(86, "读完第三章", 150, 154);
        var cursor = layer.Cursor();

        var script = new SceneScript
        {
            Reset = () =>
            {
                Motion.Hide(newRow);
                Motion.Hide(newDot);
                Motion.Hide(newName);
                newName.Text = "读书";
                Motion.Hide(renameBox);
                Motion.Show(add);
                Motion.Show(addLabel);
                Motion.Hide(inputBox);
                inputText.Text = string.Empty;
                layer.Brushes.Set(newRow, Border.BackgroundProperty, null);
                layer.Brushes.Set(work, Border.BackgroundProperty, null);
                title.Text = "全部任务";
                hint.Text = "所有任务的综合看板";
                Motion.Show(rowA.Frame);
                rowA.Reset();
                rowB.Reset();
                Motion.Place(add, 0, 0);
                Motion.Place(addLabel, 0, 0);
                cursor.Park();
            }
        };

        script.MoveTo(cursor, 60, 132)
            .Click(cursor, 60, 132)
            .Then(300, () =>
            {
                Motion.Hide(add);
                Motion.Hide(addLabel);
                Motion.Show(inputBox);
                cursor.Park();
            })
            .Type(inputText, "读书", 260)
            .Wait(400)
            .Then(500, () =>
            {
                Motion.Hide(inputBox);
                inputText.Text = string.Empty;
                Motion.Show(newRow);
                Motion.Show(newDot);
                Motion.Show(newName);
                Motion.Show(add);
                Motion.Show(addLabel);
            })
            // 单击：进入该项目，只看它的任务
            .MoveTo(cursor, 60, 108)
            .Click(cursor, 60, 108)
            .Then(1200, () =>
            {
                layer.Brushes.Set(newRow, Border.BackgroundProperty, "AccentSubtleBrush");
                title.Text = "读书";
                hint.Text = "该项目下的全部任务";
                Motion.Hide(rowA.Frame);
                Motion.Place(rowB.Frame, 0, -30);
            })
            // 双击：原地变成输入框
            .Click(cursor, 60, 108)
            .Click(cursor, 60, 108)
            .Then(300, () =>
            {
                Motion.Show(renameBox);
                cursor.Park();
            })
            .Type(newName, "读书笔记", 220, "读书")
            .Wait(1600);

        return script;
    }

    private static SceneScript DeleteProject(SceneLayer layer)
    {
        var (_, side) = layer.Group(10, 10, 132, 110, "SidebarWashBrush", 8);
        side.Micro(12, 10, "PROJECTS");
        side.Box(8, 22, 116, 20, null, 10);
        side.Dot(16, 29, 6, "PriorityMediumBrush");
        side.Text(28, 26, "工作", 9.5, "TextSecondaryBrush", FontWeight.Medium);

        var target = side.Box(8, 46, 116, 20, null, 10);
        var targetDot = side.Dot(16, 53, 6, "PriorityLowBrush");
        var targetName = side.Text(28, 50, "旧项目", 9.5, "TextSecondaryBrush", FontWeight.Medium);
        var trash = side.Icon(106, 51, 10, "IconTrash", "TextTertiaryBrush");

        var rows = new[]
        {
            layer.TaskRow(24, "旧需求 A", 148, 162),
            layer.TaskRow(52, "旧需求 B", 148, 162)
        };

        // 与真实界面一致（spec-due-date-picker G3）：居中确认框 + 压暗遮罩，列表不被推动
        var dim = layer.Box(0, 0, 320, 180, null, 0);
        dim.Background = new SolidColorBrush(Color.Parse("#73000000"));
        var (confirm, confirmLayer) = layer.Group(70, 50, 180, 78, "CardSurfaceBrush", 8, "HairlineStrongBrush");
        confirmLayer.Text(12, 10, "删除项目「旧项目」？", 10, "TextPrimaryBrush", FontWeight.SemiBold);
        confirmLayer.Text(12, 28, "将删除其下 3 条任务，不可撤销。", 8, "TextSecondaryBrush");
        confirmLayer.Cell(62, 52, 44, 18, "取消", "HairlineStrongBrush", "TextSecondaryBrush");
        var ok = confirmLayer.Cell(112, 52, 56, 18, "确认删除", "PriorityHighSurfaceBrush", "PriorityHighBrush");
        var cursor = layer.Cursor();

        var script = new SceneScript
        {
            Reset = () =>
            {
                foreach (var control in new Control[] { target, targetDot, targetName })
                {
                    Motion.Show(control);
                }

                layer.Brushes.Set(target, Border.BackgroundProperty, null);
                Motion.Hide(trash);
                layer.Brushes.Set(trash, Avalonia.Controls.Shapes.Shape.StrokeProperty, "TextTertiaryBrush");
                Motion.Hide(confirm);
                Motion.Hide(dim);
                layer.Tint(ok, "PriorityHighSurfaceBrush", "PriorityHighBrush");
                foreach (var row in rows)
                {
                    row.Reset();
                }

                cursor.Park();
            }
        };

        return script.MoveTo(cursor, 60, 66)
            .Then(500, () =>
            {
                layer.Brushes.Set(target, Border.BackgroundProperty, "HairlineStrongBrush");
                Motion.Show(trash);
            })
            .MoveTo(cursor, 121, 66)
            .Then(160, () => layer.Brushes.Set(trash, Avalonia.Controls.Shapes.Shape.StrokeProperty, "PriorityHighBrush"))
            .Click(cursor, 121, 66)
            .Then(1400, () =>
            {
                Motion.Show(dim);
                Motion.Show(confirm);
            })
            .MoveTo(cursor, 210, 111)
            .Click(cursor, 210, 111)
            .Then(400, () => layer.Tint(ok, "PriorityHighBrush", "OnAccentBrush"))
            .Then(1800, () =>
            {
                Motion.Hide(dim);
                Motion.Hide(confirm);
                foreach (var control in new Control[] { target, targetDot, targetName, trash })
                {
                    Motion.Hide(control);
                }

                foreach (var row in rows)
                {
                    Motion.Hide(row.Frame);
                }

                cursor.Park();
            });
    }

    private static SceneScript QuickCapture(SceneLayer layer)
    {
        // 背景是别的应用：示意主窗不必在前台
        var (_, editor) = layer.Group(10, 10, 300, 160, "CardSurfaceBrush", 8, "HairlineBrush");
        editor.Micro(12, 10, "正在写代码…");
        for (var i = 0; i < 6; i++)
        {
            editor.Box(12, 28 + (i * 14), 90 + ((i * 53) % 150), 5, "HairlineStrongBrush", 2.5);
        }

        var alt = layer.Key(92, 138, "Alt");
        var space = layer.Key(126, 138, "Space");
        alt.Width = 30;
        space.Width = 60;

        var (mini, miniLayer) = layer.Group(150, 34, 150, 96, "FloatingSurfaceBrush", 10, "HairlineStrongBrush");
        miniLayer.Micro(10, 9, "快捷小窗");
        miniLayer.Box(10, 22, 130, 1, "HairlineBrush", 0);
        var miniText = miniLayer.Text(10, 28, string.Empty, 9.5);
        miniLayer.Box(10, 44, 130, 1, "HairlineBrush", 0);
        var miniRow1 = miniLayer.Dot(10, 52, 8, null, "TextTertiaryBrush");
        miniLayer.Text(24, 50, "订会议室", 8.5, "TextSecondaryBrush");
        miniLayer.Dot(10, 68, 8, null, "TextTertiaryBrush");
        miniLayer.Text(24, 66, "回电话", 8.5, "TextSecondaryBrush");
        var miniNew = miniLayer.Text(24, 82, "修复登录 bug", 8.5, "AccentBrush");

        var script = new SceneScript
        {
            Reset = () =>
            {
                layer.Press(alt, false);
                layer.Press(space, false);
                Motion.Hide(mini);
                Motion.Place(mini, 0, 14, 0.92);
                miniText.Text = string.Empty;
                Motion.Hide(miniNew);
                layer.Brushes.Set(miniRow1, Avalonia.Controls.Shapes.Shape.StrokeProperty, "TextTertiaryBrush");
            }
        };

        return script.Wait(700)
            .Then(260, () => layer.Press(alt, true))
            .Then(220, () => layer.Press(space, true))
            .Then(520, () =>
            {
                Motion.Show(mini);
                Motion.Place(mini, 0, 0);
            })
            .Then(200, () =>
            {
                layer.Press(alt, false);
                layer.Press(space, false);
            })
            .Type(miniText, "修复登录 bug", 110)
            .Then(700, () =>
            {
                miniText.Text = string.Empty;
                Motion.Show(miniNew);
            })
            .Wait(900)
            .Then(260, () => layer.Press(alt, true))
            .Then(220, () => layer.Press(space, true))
            .Then(1100, () =>
            {
                layer.Press(alt, false);
                layer.Press(space, false);
                Motion.Hide(mini);
                Motion.Place(mini, 0, 14, 0.92);
            });
    }

    private static SceneScript QuickWindowKeys(SceneLayer layer)
    {
        var (miniFrame, mini) = layer.Group(60, 12, 200, 118, "FloatingSurfaceBrush", 10, "HairlineStrongBrush");
        var chips = new[]
        {
            mini.Pill(10, 10, 46, "Default", "AccentSubtleBrush", "AccentBrush"),
            mini.Pill(60, 10, 38, "工作", null, "TextTertiaryBrush"),
            mini.Pill(102, 10, 38, "读书", null, "TextTertiaryBrush")
        };
        mini.Box(10, 34, 180, 1, "HairlineBrush", 0);
        var text = mini.Text(10, 42, "Something to do...", 10, "TextTertiaryBrush");
        mini.Box(10, 60, 180, 1, "HairlineBrush", 0);
        var saved = mini.Text(24, 68, "写读书笔记", 9, "TextSecondaryBrush");
        var savedDot = mini.Dot(10, 70, 8, null, "TextTertiaryBrush");

        var keys = new[]
        {
            (Key: layer.Key(50, 144, "Ctrl+Tab"), Label: layer.Text(50, 166, "切换项目", 8, "TextTertiaryBrush")),
            (Key: layer.Key(144, 144, "↵"), Label: layer.Text(140, 166, "保存", 8, "TextTertiaryBrush")),
            (Key: layer.Key(222, 144, "Esc"), Label: layer.Text(222, 166, "收起", 8, "TextTertiaryBrush"))
        };

        void Select(int index)
        {
            for (var i = 0; i < chips.Length; i++)
            {
                layer.Tint(chips[i], i == index ? "AccentSubtleBrush" : null, i == index ? "AccentBrush" : "TextTertiaryBrush");
            }
        }

        void Emphasize(int index)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                layer.Press(keys[i].Key, i == index);
                layer.Brushes.Set(keys[i].Label, TextBlock.ForegroundProperty, i == index ? "AccentBrush" : "TextTertiaryBrush");
            }
        }

        var script = new SceneScript
        {
            Reset = () =>
            {
                Select(0);
                Emphasize(-1);
                text.Text = "Something to do...";
                layer.Brushes.Set(text, TextBlock.ForegroundProperty, "TextTertiaryBrush");
                Motion.Hide(saved);
                Motion.Hide(savedDot);
                Motion.Show(miniFrame);
                Motion.Place(miniFrame, 0, 0);
            }
        };

        return script.Wait(600)
            .Then(300, () => Emphasize(0))
            .Then(500, () => { Select(1); Emphasize(-1); })
            .Then(300, () => Emphasize(0))
            .Then(500, () => { Select(2); Emphasize(-1); })
            .Then(200, () =>
            {
                text.Text = string.Empty;
                layer.Brushes.Set(text, TextBlock.ForegroundProperty, "TextPrimaryBrush");
            })
            .Type(text, "写读书笔记", 140)
            .Then(300, () => Emphasize(1))
            .Then(900, () =>
            {
                Emphasize(-1);
                text.Text = string.Empty;
                Motion.Show(saved);
                Motion.Show(savedDot);
            })
            .Then(300, () => Emphasize(2))
            .Then(1400, () =>
            {
                Emphasize(-1);
                Motion.Hide(miniFrame);
                Motion.Place(miniFrame, 0, 10);
            });
    }

    private static SceneScript DefaultDue(SceneLayer layer)
    {
        var (_, card) = layer.Group(12, 12, 296, 64, "CardSurfaceBrush", 8);
        card.Micro(12, 10, "默认到期偏移");
        card.Box(12, 26, 160, 24, null, 4, "HairlineStrongBrush");
        var days = card.Text(22, 31, "1", 11, "TextPrimaryBrush", FontWeight.SemiBold);
        card.Cell(126, 29, 18, 18, "−", "HairlineBrush", "TextSecondaryBrush");
        card.Cell(148, 29, 18, 18, "+", "HairlineBrush", "TextSecondaryBrush");
        card.Cell(186, 28, 48, 20, "保存", "AccentBrush", "OnAccentBrush", 10, 8.5);
        // 卡片左上角在 (12, 12)，以下为场景绝对坐标
        const double plusX = 12 + 157;
        const double saveX = 12 + 210;
        var savedHint = card.Text(242, 32, "已保存", 8.5, "AccentBrush");

        layer.Text(12, 94, "新任务", 12, "TextPrimaryBrush", FontWeight.SemiBold);
        // 日期框里的「默认 +N 天」预设（spec-due-date-picker），N 跟随上方设置
        var enable = layer.Pill(12, 116, 70, "默认 +3 天", "HairlineStrongBrush", "TextSecondaryBrush");
        var result = layer.Text(92, 118, "到期", 9.5, "TextTertiaryBrush");
        var formula = layer.Text(12, 146, "今天 + 3 天", 10, "AccentBrush", FontWeight.SemiBold);
        var cursor = layer.Cursor();

        var script = new SceneScript
        {
            Reset = () =>
            {
                days.Text = "1";
                Motion.Hide(savedHint);
                result.Text = "到期";
                layer.Brushes.Set(result, TextBlock.ForegroundProperty, "TextTertiaryBrush");
                layer.Tint(enable, "HairlineStrongBrush", "TextSecondaryBrush");
                Motion.Hide(formula);
                Motion.Place(formula, 0, 6);
                cursor.Park();
            }
        };

        return script.MoveTo(cursor, plusX, 50)
            .Click(cursor, plusX, 50)
            .Then(300, () => days.Text = "2")
            .Click(cursor, plusX, 50)
            .Then(500, () => days.Text = "3")
            .MoveTo(cursor, saveX, 50)
            .Click(cursor, saveX, 50)
            .Then(900, () => Motion.Show(savedHint))
            .MoveTo(cursor, 60, 128)
            .Click(cursor, 60, 128)
            .Then(1800, () =>
            {
                layer.Tint(enable, "AccentSubtleBrush", "AccentBrush");
                result.Text = "3 天后到期";
                layer.Brushes.Set(result, TextBlock.ForegroundProperty, "AccentBrush");
                Motion.Show(formula);
                Motion.Place(formula, 0, 0);
                cursor.Park();
            });
    }

    private static SceneScript CloseToTray(SceneLayer layer)
    {
        var (window, win) = layer.Group(20, 10, 220, 128, "CardSurfaceBrush", 8, "HairlineStrongBrush");
        win.Micro(10, 8, "FLOWTASK");
        var close = win.Icon(200, 6, 11, "IconClose", "TextTertiaryBrush");
        for (var i = 0; i < 4; i++)
        {
            win.Dot(12, 32 + (i * 22), 9, null, "TextTertiaryBrush");
            win.Box(28, 34 + (i * 22), 60 + ((i * 37) % 90), 5, "HairlineStrongBrush", 2.5);
        }

        // 任务栏与托盘区
        layer.Box(0, 156, 320, 24, "SidebarWashBrush", 0);
        var tray = layer.Box(284, 160, 16, 16, "AccentSubtleBrush", 4);
        var trayGlyph = layer.Icon(287, 163, 10, "IconCheck", "AccentBrush");
        var (menu, menuLayer) = layer.Group(212, 90, 96, 62, "FloatingSurfaceBrush", 6, "HairlineStrongBrush");
        menuLayer.Text(10, 8, "显示小窗", 8.5, "TextSecondaryBrush");
        menuLayer.Box(8, 26, 80, 1, "HairlineBrush", 0);
        menuLayer.Text(10, 34, "退出", 8.5, "TextSecondaryBrush");

        var hint = layer.Text(20, 142, "快捷键依然有效", 8.5, "AccentBrush");
        var cursor = layer.Cursor();

        var script = new SceneScript
        {
            Reset = () =>
            {
                Motion.Show(window);
                Motion.Place(window, 0, 0);
                layer.Brushes.Set(close, Avalonia.Controls.Shapes.Shape.StrokeProperty, "TextTertiaryBrush");
                Motion.Hide(menu);
                Motion.Hide(hint);
                Motion.Place(tray, 0, 0);
                Motion.Place(trayGlyph, 0, 0);
                cursor.Park();
            }
        };

        return script.MoveTo(cursor, 225, 22)
            .Then(200, () => layer.Brushes.Set(close, Avalonia.Controls.Shapes.Shape.StrokeProperty, "PriorityHighBrush"))
            .Click(cursor, 225, 22)
            .Then(600, () =>
            {
                // 主窗缩向托盘图标
                Motion.Hide(window);
                Motion.Place(window, 150, 110, 0.2);
            })
            .Then(300, () =>
            {
                Motion.Place(tray, 0, -3);
                Motion.Place(trayGlyph, 0, -3);
                Motion.Show(hint);
            })
            .Then(700, () =>
            {
                Motion.Place(tray, 0, 0);
                Motion.Place(trayGlyph, 0, 0);
            })
            .MoveTo(cursor, 292, 170)
            .Click(cursor, 292, 170)
            .Then(1400, () => Motion.Show(menu))
            .Then(400, () => Motion.Hide(menu))
            .Click(cursor, 292, 170)
            .Then(1500, () =>
            {
                Motion.Show(window);
                Motion.Place(window, 0, 0);
                cursor.Park();
            });
    }

    private static SceneScript Theme(SceneLayer layer)
    {
        // 场景自带一套"反相"配色：演示昼夜切换，但不真的改应用主题
        var (surface, content) = layer.Group(0, 0, StageWidth, StageHeight, "CardSurfaceBrush", 0);
        content.Micro(16, 14, "所有任务的综合看板", "AccentBrush");
        var heading = content.Text(16, 26, "全部任务", 17, "TextPrimaryBrush", FontWeight.Bold);
        var lines = Enumerable.Range(0, 4)
            .Select(i => content.Box(16, 72 + (i * 22), 120 + ((i * 41) % 100), 6, "HairlineStrongBrush", 3))
            .ToArray();

        var help = content.Box(208, 16, 28, 28, null, 14);
        content.Icon(213, 21, 18, "IconHelp", "TextSecondaryBrush");
        var gear = content.Box(240, 16, 28, 28, null, 14);
        content.Icon(245, 21, 18, "IconSettings", "TextSecondaryBrush");
        var themeButton = content.Box(272, 16, 28, 28, null, 14);
        var sun = content.Icon(277, 21, 18, "IconSun", "TextSecondaryBrush");
        var moon = content.Icon(277, 21, 18, "IconMoon", "TextSecondaryBrush");

        // 波纹颜色即切换后的底色（场景里用 TextPrimary 充当"反相"底色）
        var ripple = content.Dot(286 - 360, 30 - 360, 720, "TextPrimaryBrush");
        ripple.RenderTransformOrigin = Avalonia.RelativePoint.Center;
        ripple.IsHitTestVisible = false;
        // 压在按钮与文字之下、底色之上：Canvas 按添加顺序绘制，ZIndex 0 的前序元素须调高
        foreach (var child in content.Canvas.Children)
        {
            if (!ReferenceEquals(child, ripple))
            {
                child.ZIndex = 1;
            }
        }

        var labels = new[]
        {
            (Target: help, Label: content.Text(198, 50, "操作指南", 8, "AccentBrush", FontWeight.SemiBold)),
            (Target: gear, Label: content.Text(240, 50, "设置", 8, "AccentBrush", FontWeight.SemiBold)),
            (Target: themeButton, Label: content.Text(272, 50, "昼夜", 8, "AccentBrush", FontWeight.SemiBold))
        };
        foreach (var (_, label) in labels)
        {
            label.ZIndex = 1;
        }

        var cursor = layer.Cursor();

        void Light(bool light)
        {
            layer.Brushes.Set(surface, Border.BackgroundProperty, light ? "TextPrimaryBrush" : "CardSurfaceBrush");
            layer.Brushes.Set(heading, TextBlock.ForegroundProperty, light ? "CardSurfaceBrush" : "TextPrimaryBrush");
            foreach (var line in lines)
            {
                line.Opacity = light ? 0.35 : 1;
            }

            Motion.Show(sun, !light);
            Motion.Show(moon, light);
        }

        void Focus(int index)
        {
            for (var i = 0; i < labels.Length; i++)
            {
                layer.Brushes.Set(labels[i].Target, Border.BackgroundProperty, i == index ? "AccentSubtleBrush" : null);
                Motion.Show(labels[i].Label, i == index);
            }
        }

        var script = new SceneScript
        {
            Reset = () =>
            {
                Light(false);
                Focus(-1);
                // 无过渡归零，避免复位时反向收缩被看见
                ripple.Transitions = null;
                Motion.Place(ripple, 0, 0, 0);
                ripple.Opacity = 0;
                cursor.Park();
            }
        };

        return script.MoveTo(cursor, 286, 30)
            .Click(cursor, 286, 30)
            .Then(40, () =>
            {
                ripple.Opacity = 1;
                ripple.Transitions = new Avalonia.Animation.Transitions
                {
                    new Avalonia.Animation.TransformOperationsTransition
                    {
                        Property = Avalonia.Visual.RenderTransformProperty,
                        Duration = Motion.Ripple,
                        Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                    },
                    new Avalonia.Animation.DoubleTransition
                    {
                        Property = Avalonia.Visual.OpacityProperty,
                        Duration = Motion.Fade,
                        Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                    }
                };
            })
            // 水波纹从按钮扩散覆盖，覆盖满后才换色（design-visual-language §5.1：扩散 520ms、淡出 260ms）
            .Then(540, () => Motion.Place(ripple, 0, 0, 1))
            .Then(300, () =>
            {
                Light(true);
                ripple.Opacity = 0;
            })
            .Then(900, () => cursor.Park())
            .Then(1100, () => Focus(1))
            .Then(1500, () => Focus(0))
            .Wait(1200);
    }
}
