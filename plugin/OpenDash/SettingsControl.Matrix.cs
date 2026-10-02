// SettingsControl.Matrix.cs: the Matrix page, as Matrix.dc.html draws it -- the flag box profile on the
// title's line, a card for each matrix, and for the one selected its preview beside what may take it over and
// what it shows at rest.
//
// Every word, number and decision is PanelMatrix's; this file only draws. The profile's state is asked of
// SimHub whenever the shell asks what needs fixing -- opening the page, a press that redraws it, Check again,
// a return to the panel -- and only a press writes it (ADR 0013). The build reads SimHub's matrix profiles, so
// it never asks to be rebuilt by a wheel's lighting press: every picture re-dims in place through OnLighting.
// docs/design/flag-box.md is what the box draws.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The chip the preview is on, kept for the session across builds and matrices.</summary>
        private string matrixPreviewScenario = PanelMatrix.IdleScenario;

        /// <summary>
        /// What SimHub holds for the flag box, kept for as long as the shell's answer to what needs fixing
        /// (<see cref="issues"/>) that it was read beside: asking parses the whole embedded profile, and a card,
        /// a side or Shift colours rebuilds the page without asking. Every time the shell asks SimHub again --
        /// Go, Redraw after a press, Check again, and the return to the panel from SimHub (CatchUp) -- it
        /// replaces that list, and the page asks again with it, so the title's line never disagrees with the
        /// sidebar's dot or Home.
        /// </summary>
        private FlagBoxPlan matrixPlan;
        private IList<PanelIssue> matrixPlanAsked;

        /// <summary>
        /// A failed press's own result, which asking SimHub again cannot see (the plan never reads Failed), so
        /// the title's line says "Install failed" through the redraw the press starts and any rebuild in place
        /// after it. It is let go of when the shell asks again for a reason of its own (a return to the panel,
        /// another press, Check again) and when the page is left. <see cref="matrixFailedAsked"/> is null until
        /// the press's redraw adopts the answer it asked for.
        /// </summary>
        private FlagBoxPlan matrixFailed;
        private IList<PanelIssue> matrixFailedAsked;

        /// <summary>The state the failed press was made in, which the press offered after it is warned as
        /// (PanelMatrix.ProfileTooltip): SimHub may still hold the copy that press was made over.</summary>
        private FlagBoxInstallState matrixFailedFrom = FlagBoxInstallState.NotInstalled;

        /// <summary>
        /// Set by a sheet's press, which closes the sheet and draws the page again under the pointer: the next
        /// press on the page is then the second of a double-click when WPF counts it so, and is ignored. WPF
        /// counts clicks by time and place, not by element, so without this it would land on whatever switch the
        /// new page puts there -- after Remove it, on another matrix's -- and flip it. Cleared by the next press
        /// that is not a repeat, so every further press of a triple-click is ignored as well.
        /// </summary>
        private bool matrixSheetPressed;

        /// <summary>The page's guard for <see cref="matrixSheetPressed"/>, on the page's root.</summary>
        private void MatrixIgnoreRepeat(object sender, MouseButtonEventArgs args)
        {
            // Armed for as long as the presses keep counting: a triple-click's third press is a repeat too.
            if (matrixSheetPressed && args.ClickCount > 1)
            {
                args.Handled = true;
                return;
            }
            matrixSheetPressed = false;
        }

        private FlagBoxPlan MatrixPlan()
        {
            if (plugin.FlagBoxJson == null) return null;
            if (matrixFailed != null)
            {
                if (matrixFailedAsked == null) matrixFailedAsked = issues;
                if (ReferenceEquals(matrixFailedAsked, issues)) return matrixFailed;
                matrixFailed = null;
            }
            if (matrixPlan == null || !ReferenceEquals(matrixPlanAsked, issues))
            {
                matrixPlan = SafePlan();
                matrixPlanAsked = issues;
            }
            return matrixPlan;
        }

        private FrameworkElement BuildMatrixPage(PanelRoute to)
        {
            // No DrawsLighting(): the build reads the flag box plan from SimHub (SafePlan).
            OnLeave("Matrix.plan", () =>
            {
                matrixPlan = null;
                matrixFailed = null;
            });
            var plan = MatrixPlan();
            var panels = Settings.MatrixPanels().ToList();
            var slot = PanelMatrix.SelectedSlot(panels, Selected(PanelPage.Matrix));
            Border selectedCard = null;
            var cards = BuildMatrixCards(panels, slot, picture => selectedCard = picture);
            // The profile on the title's line; under the title the by-hand import, only when SimHub's matrix
            // settings could not be reached (the shell's, which Matrix and Updates both draw); the cards; and
            // the selected matrix.
            var page = PageLayout(PanelMatrix.Title,
                Ui.Anchor(BuildMatrixProfile(plan), PanelMatrix.AnchorProfile),
                PanelMatrix.ShowsImportFallback(PanelMatrix.StateOf(plan)) ? BuildFlagBoxImportFallback(plan) : null,
                Ui.Anchor(cards, PanelMatrix.AnchorPanels),
                slot == 0 ? null : BuildMatrixSelected(slot, selectedCard));
            page.PreviewMouseLeftButtonDown += MatrixIgnoreRepeat;
            return page;
        }

        /// <summary>
        /// The flag box profile on the title's line: a dot in its state's ink (PanelMatrix.ProfileRow), the
        /// profile as SimHub lists it with its version, and the one press that changes it, which is left out
        /// when there is nothing to install or nowhere to put it.
        /// </summary>
        private FrameworkElement BuildMatrixProfile(FlagBoxPlan plan)
        {
            var state = PanelMatrix.StateOf(plan);
            var version = plan == null ? null : plan.InstalledVersion;
            var action = PanelMatrix.ProfileRow(state, version);
            // The dot is the page's own ink for the state, the one part of the line that tells an older profile
            // from a current one: never another page's table, which has moved under it twice.
            var dot = new Ellipse
            {
                Width = PanelMatrix.ProfileDotSize,
                Height = PanelMatrix.ProfileDotSize,
                Fill = Ui.Brush(action.StateHex),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var line = Ui.Text(PanelMatrix.ProfileLine(FlagBoxName(), state, version), Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
            line.VerticalAlignment = VerticalAlignment.Center;
            var row = Ui.HStack(PanelMatrix.ProfileGap, dot, line);
            if (PanelMatrix.ProfileHasButton(state))
            {
                var primary = action.Style == PanelButton.Primary;
                var button = Ui.Button(action.Button, primary ? PanelButtonKind.Primary : PanelButtonKind.Ghost, PanelButtonSize.Small);
                if (!primary) button.Padding = new Thickness(PanelMatrix.ProfileButtonPaddingX, 0, PanelMatrix.ProfileButtonPaddingX, 0);
                button.ToolTip = PanelMatrix.ProfileTooltip(state, matrixFailedFrom);
                // The redraw puts the next state's press where this one was and gives it the keyboard's focus,
                // so the second press of a double-click, or Enter held down, would install again: a second
                // write to SimHub, and a message that says Reinstalled over the one that gave the select step.
                // Only a fresh press counts.
                button.PreviewMouseLeftButtonDown += (sender, args) => { if (args.ClickCount > 1) args.Handled = true; };
                button.PreviewKeyDown += (sender, args) => { if (args.Key == Key.Enter && args.IsRepeat) args.Handled = true; };
                button.Click += (sender, args) =>
                {
                    var from = PanelMatrix.PressedFrom(state, matrixFailedFrom);
                    var result = InstallFlagBox();
                    // Asked again on the redraw, unless it failed, which asking again cannot see.
                    matrixFailed = PanelMatrix.KeepsPressResult(result.State) ? result : null;
                    matrixFailedAsked = null;
                    matrixFailedFrom = from;
                    // Redraw asks what needs fixing again, so the sidebar's dot and Home move with it.
                    Redraw();
                    // One message, the installer's note inside it in the order it has to be done.
                    var said = PanelMatrix.InstallSaid(from, state, result.State, FlagBoxName(), result.Note, Settings.MatrixPanels().ToList());
                    if (said != null) Say(said);
                };
                row.Children.Add(button);
            }
            // Nothing to hover where the line, or the by-hand import under it, already says it all.
            var hover = PanelMatrix.ProfileLineTooltip(state, plan == null ? null : plan.EmbeddedVersion, FlagBoxName(), Settings.MatrixPanels().ToList());
            if (hover != null) row.ToolTip = hover;
            row.HorizontalAlignment = HorizontalAlignment.Right;
            return row;
        }

        /// <summary>
        /// A card for each matrix, as its idle display draws it, and the tile that adds one, which fades at
        /// four. <paramref name="selectedPicture"/> is handed the selected card's picture, which the selected
        /// matrix's switches repaint in place.
        /// </summary>
        private FrameworkElement BuildMatrixCards(IList<int> panels, int selected, Action<Border> selectedPicture)
        {
            var cards = new List<UIElement>();
            foreach (var matrix in panels)
            {
                var m = matrix;
                var facts = MatrixFacts(m);
                var shown = facts == null ? null : facts.Shown;
                var name = PanelMatrix.NameOf(Settings.MatrixName(m), m);
                var picture = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, PanelMatrix.OptionsFor(Settings, m)), MatrixStyle.Card, MatrixDim());
                OnLighting(() => Ui.Redim(picture, MatrixDim()));
                if (m == selected) selectedPicture(picture);
                var card = Ui.MatrixCard(picture, name, PanelMatrix.CardLine(name, m, Settings.MatrixSide(m), shown),
                    PanelMatrix.CardLineHex(shown), m == selected, () =>
                    {
                        Select(PanelPage.Matrix, PanelMatrix.SlotId(m));
                        RebuildPage();
                    });
                // The card trims a long name to its column; the hover gives it whole. A screen reader is told
                // the name and the line under it, since the card's content is a picture and two lines, which it
                // cannot read.
                card.ToolTip = name;
                AutomationProperties.SetName(card, PanelMatrix.CardName(name, m, Settings.MatrixSide(m), shown));
                cards.Add(card);
            }
            var add = Ui.InlineAddCard(PanelMatrix.AddPanel, PanelKit.MatrixAddIcon, ShowAddMatrix, PanelMatrix.AddCount(panels.Count));
            AutomationProperties.SetName(add, PanelMatrix.AddPanel);
            if (PanelMatrix.AddEnabled(panels.Count, Settings.FreeMatrixSlot()))
            {
                add.ToolTip = PanelMatrix.AddTooltip;
            }
            else
            {
                add.IsEnabled = false;
                add.ToolTip = PanelMatrix.AllInUse;
                ToolTipService.SetShowOnDisabled(add, true);
            }
            // The artboard's tablist "Your matrices", as a group a screen reader is told about: the grid itself
            // is a panel, which it never sees.
            cards.Add(add);
            var grid = new MatrixNamed(AutomationControlType.Group) { Child = Ui.CardGrid(PanelMatrix.CardMinWidth, PanelMatrix.CardGap, PanelMatrix.CardColumns, cards.ToArray()) };
            AutomationProperties.SetName(grid, PanelMatrix.PanelsTitle);
            if (panels.Count > 0) return grid;
            // An empty rig names the emptiness beside the one press there is.
            return Ui.VStack(PanelMatrix.EmptyGap, Ui.Caption(PanelMatrix.NoPanels), grid);
        }

        /// <summary>
        /// The selected matrix: its name and content number with Rename and Remove, the fix box when something
        /// has read that no device shows it, and its preview beside the priority list.
        /// </summary>
        private FrameworkElement BuildMatrixSelected(int matrix, Border cardPicture)
        {
            var m = matrix;
            var name = PanelMatrix.NameOf(Settings.MatrixName(m), m);
            var title = Ui.SubHeading(name);
            // A long name wraps rather than trimming: nothing limits a name's length, and the Rename box was
            // otherwise the only place it could be read in full. A WrapPanel, not a horizontal StackPanel, so
            // the name is measured at the column's width, and the content number wraps under a long one
            // rather than being cut off.
            title.TextTrimming = TextTrimming.None;
            title.TextWrapping = TextWrapping.Wrap;
            var heading = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            heading.Children.Add(title);
            var slotCaption = PanelMatrix.SlotCaption(name, m);
            if (slotCaption != null)
            {
                // The gap is the name's, not the number's: a number that wraps under a long name starts at the
                // column's edge rather than 12 in from it.
                title.Margin = new Thickness(0, 0, PanelMatrix.HeaderGap, 0);
                var slot = Ui.Text(slotCaption, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
                slot.VerticalAlignment = VerticalAlignment.Bottom;
                slot.Margin = new Thickness(0, 0, 0, PanelMatrix.SlotCaptionLift);
                heading.Children.Add(slot);
            }
            var rename = Ui.Button(PanelMatrix.Rename, PanelButtonKind.Outline, PanelButtonSize.Small);
            rename.ToolTip = PanelMatrix.RenameTooltip;
            rename.Click += (sender, args) => ShowRenameMatrix(m);
            var remove = Ui.Button(PanelMatrix.Remove, PanelButtonKind.GhostDanger, PanelButtonSize.Small);
            remove.ToolTip = PanelMatrix.RemoveTooltip;
            remove.Click += (sender, args) => ShowRemoveMatrix(m);
            var head = Ui.Row(heading, Ui.HStack(PanelMatrix.ActionGap, rename, remove));

            var parts = new List<UIElement> { head };
            var facts = MatrixFacts(m);
            if (facts != null && facts.Shown == false)
            {
                var check = Ui.Button(PanelAttention.CheckAgain, PanelButtonKind.Outline, PanelButtonSize.Small);
                check.Click += (sender, args) => CheckAgain();
                var fix = Ui.FixBox(PanelMatrix.FixTitle, null, PanelMatrix.FixSteps(m, FlagBoxName()), check);
                fix.Padding = new Thickness(PanelKit.FixPaddingX, PanelKit.FixPaddingYLights, PanelKit.FixPaddingX, PanelKit.FixPaddingYLights);
                parts.Add(fix);
            }

            var previewColumn = BuildMatrixPreview(m, cardPicture);
            var priority = BuildMatrixPriority(m, previewColumn.Repaint);
            if (TwoColumns)
            {
                var body = new Grid();
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.PreviewColumnWidth) });
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.BodyGap) });
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                previewColumn.Element.VerticalAlignment = VerticalAlignment.Top;
                priority.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(previewColumn.Element, 0);
                Grid.SetColumn(priority, 2);
                body.Children.Add(previewColumn.Element);
                body.Children.Add(priority);
                parts.Add(body);
            }
            else
            {
                previewColumn.Element.MaxWidth = PanelMatrix.PreviewColumnWidth;
                previewColumn.Element.HorizontalAlignment = HorizontalAlignment.Left;
                parts.Add(Ui.VStack(PanelMatrix.StackedGap, previewColumn.Element, priority));
            }

            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, PanelMatrix.SectionPaddingTop, 0, 0),
                Child = Ui.VStack(PanelMatrix.SectionGap, parts.ToArray()),
            };
        }

        /// <summary>The preview column, and the repaint its matrix's switches call when its picture moves.</summary>
        private sealed class MatrixPreviewColumn
        {
            public MatrixPreviewColumn(FrameworkElement element, Action repaint)
            {
                Element = element;
                Repaint = repaint;
            }

            public FrameworkElement Element { get; private set; }
            public Action Repaint { get; private set; }
        }

        /// <summary>
        /// A border a screen reader is told about, as the control type it is given: WPF gives a Border, a
        /// WrapPanel or a custom panel no automation peer, so a name set on one is never heard. The artboard's
        /// role=img preview, its "Your matrices" and its chip group are these.
        /// </summary>
        private sealed class MatrixNamed : Border
        {
            private readonly AutomationControlType type;

            public MatrixNamed(AutomationControlType type)
            {
                this.type = type;
            }

            protected override AutomationPeer OnCreateAutomationPeer()
            {
                return new Peer(this, type);
            }

            private sealed class Peer : FrameworkElementAutomationPeer
            {
                private readonly AutomationControlType type;

                public Peer(MatrixNamed owner, AutomationControlType type) : base(owner)
                {
                    this.type = type;
                }

                protected override AutomationControlType GetAutomationControlTypeCore()
                {
                    return type;
                }

                protected override string GetClassNameCore()
                {
                    return "Border";
                }
            }
        }

        /// <summary>
        /// The 8x8 at the size of the real thing's cells under its New tag, what it shows under the chip chosen
        /// (its frame named for a screen reader as the artboard's alt text), the link to every device at once,
        /// and the chips. A chip repaints the picture and the chips in place.
        /// </summary>
        private MatrixPreviewColumn BuildMatrixPreview(int matrix, Border cardPicture)
        {
            var m = matrix;
            var preview = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), PanelMatrix.OptionsFor(Settings, m)), MatrixStyle.Preview, MatrixDim());
            OnLighting(() => Ui.Redim(preview, MatrixDim()));
            preview.HorizontalAlignment = HorizontalAlignment.Center;
            // The frame is the picture a screen reader is told about, the artboard's role=img.
            var frame = new MatrixNamed(AutomationControlType.Image)
            {
                Padding = new Thickness(PanelMatrix.PreviewFramePadding),
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Child = preview,
            };
            AutomationProperties.SetName(frame, PanelMatrix.PreviewAlt(MatrixDrawn(m), PanelMatrix.OptionsFor(Settings, m)));

            var all = Ui.LinkButton(PanelMatrix.AllDevices);
            all.FontSize = Theme.SizeSmall;
            all.Height = double.NaN;
            all.HorizontalAlignment = HorizontalAlignment.Left;
            all.Click += (sender, args) => Open(PanelPage.Rig, PanelMatrix.PreviewScenario(matrixPreviewScenario, PanelMatrix.OptionsFor(Settings, m)));
            // New in this release (the Map's "Live 8×8 preview", ruling 7). The preview has no title to follow
            // and the frame's padding is narrower than the tag is tall, so the tag has the frame's own line,
            // over it and clear of the lamps, rather than following the link to Rig.
            var tag = Ui.NewTag();
            tag.HorizontalAlignment = HorizontalAlignment.Left;
            var tagged = Ui.VStack(PanelMatrix.NewTagGap, tag, frame);

            var chips = new WrapPanel { Orientation = Orientation.Horizontal };
            var chipGroup = new MatrixNamed(AutomationControlType.Group) { Child = chips };
            AutomationProperties.SetName(chipGroup, PanelMatrix.PreviewChipsName);
            Action drawChips = null;
            Action repaint = () =>
            {
                var options = PanelMatrix.OptionsFor(Settings, m);
                MatrixRepaint(preview, PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), options), MatrixStyle.Preview);
                AutomationProperties.SetName(frame, PanelMatrix.PreviewAlt(MatrixDrawn(m), options));
                if (cardPicture != null) MatrixRepaint(cardPicture, PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, options), MatrixStyle.Card);
            };
            drawChips = () =>
            {
                // The chips this matrix offers, read as they are drawn: a change to the idle display rebuilds
                // the page, and with it the chips.
                var offered = PanelMatrix.PreviewChips(PanelMatrix.OptionsFor(Settings, m));
                var scenario = PanelMatrix.PreviewScenario(matrixPreviewScenario, PanelMatrix.OptionsFor(Settings, m));
                var focused = -1;
                for (var i = 0; i < chips.Children.Count; i++)
                {
                    if (chips.Children[i].IsKeyboardFocusWithin) focused = i;
                }
                chips.Children.Clear();
                foreach (var id in offered)
                {
                    var chosen = id;
                    var chip = Ui.Chip(PanelMatrix.PreviewLabel(chosen), chosen == scenario, () =>
                    {
                        matrixPreviewScenario = chosen;
                        repaint();
                        drawChips();
                    });
                    chip.Padding = new Thickness(PanelKit.ChipPaddingXLights, 0, PanelKit.ChipPaddingXLights, 0);
                    chip.Margin = new Thickness(0, 0, PanelMatrix.ChipGap, PanelMatrix.ChipGap);
                    chips.Children.Add(chip);
                }
                if (focused >= 0 && focused < chips.Children.Count) chips.Children[focused].Focus();
            };
            drawChips();

            var column = Ui.VStack(PanelMatrix.PreviewGap, tagged, all, chipGroup);
            return new MatrixPreviewColumn(Ui.Anchor(column, PanelMatrix.AnchorPreview), repaint);
        }

        /// <summary>The scenario the preview draws under the chip it is on, as the matrix's settings leave it.</summary>
        private string MatrixDrawn(int matrix)
        {
            var options = PanelMatrix.OptionsFor(Settings, matrix);
            return PanelMatrix.DrawnScenario(PanelMatrix.PreviewScenario(matrixPreviewScenario, options), options, Settings.MatrixCriticalOnly(matrix));
        }

        /// <summary>Puts a fresh frame's lamps into a picture already on the page, so what re-dims it through
        /// OnLighting keeps hold of it.</summary>
        private void MatrixRepaint(Border picture, IList<string> cells, MatrixStyle style)
        {
            var fresh = Ui.Matrix(cells, style, MatrixDim());
            var lamps = fresh.Child;
            fresh.Child = null;
            picture.Child = lamps;
        }

        private double MatrixDim()
        {
            return PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);
        }

        /// <summary>
        /// What may take the matrix over, in the flag box's order, each with its switch and the options that
        /// belong to it; then what it shows when nothing does, and the device row #363 builds.
        /// </summary>
        private FrameworkElement BuildMatrixPriority(int matrix, Action repaint)
        {
            var m = matrix;
            var i = PanelMatrix.Index(m);

            var reorder = Ui.HStack(PanelMatrix.ReorderGap,
                Ui.Text(PanelMatrix.DragToReorder, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary),
                Ui.SoonTag(PanelSoon.PriorityOrder));
            var head = Ui.Row(Ui.Heading(PanelMatrix.PriorityTitle), Ui.Soon(reorder, PanelSoon.PriorityOrder));
            head.Margin = new Thickness(0, 0, 0, PanelMatrix.PriorityHeadGap);

            var flags = MatrixLayer(
                MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.FlagsTitle), PanelMatrix.FlagsTitle, null, null,
                    BuildToggle(Settings.MatrixFlags(m), on => { Settings.FlagBoxFlags[i] = on; Save(); repaint(); })),
                MatrixOption(PanelMatrix.CriticalFlagsOnlyTitle, null,
                    BuildToggle(Settings.MatrixCriticalOnly(m), on => { Settings.FlagBoxMatrixCriticalOnly[i] = on; Save(); repaint(); })));

            var pit = MatrixLayer(
                MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.PitLaneTitle), PanelMatrix.PitLaneTitle, null, null,
                    BuildToggle(Settings.MatrixPit(m), on => { Settings.FlagBoxPit[i] = on; Save(); repaint(); })));

            var side = BuildSegmented(PanelMatrix.SideValues, PanelMatrix.SideLabels, Settings.MatrixSide(m), value =>
            {
                Settings.FlagBoxSide[i] = value;
                Save();
                // The card's line names the side, so the page is drawn again rather than the picture alone.
                RebuildPage();
            }, PanelKit.SegmentedHeightMatrix);
            var spotter = MatrixLayer(
                MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.SpotterTitle), PanelMatrix.SpotterTitle, null, null,
                    BuildToggle(Settings.MatrixSpotter(m), on => { Settings.FlagBoxSpotter[i] = on; Save(); repaint(); })),
                MatrixOption(PanelMatrix.MountingSideTitle, null, side),
                // The rig's slide or snap, which every matrix shares.
                Ui.Anchor(MatrixOption(PanelMatrix.SpotterAnimationTitle, PanelMatrix.SpotterAnimationCaption,
                    BuildToggle(Settings.FlagBoxSpotterAnimation, on => { Settings.FlagBoxSpotterAnimation = on; Save(); })), PanelMatrix.AnchorSpotterAnimation));

            // The thresholds are the rig's, on Settings' Alerts, and never per matrix.
            var thresholds = Ui.LinkButton(PanelMatrix.ThresholdsLink);
            thresholds.FontSize = Theme.SizeSmall;
            thresholds.Height = double.NaN;
            thresholds.Click += (sender, args) => Go(PanelMatrix.ThresholdsRoute);
            var warnings = MatrixLayer(
                MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.WarningsTitle), PanelMatrix.WarningsTitle, PanelMatrix.WarningsCaption, thresholds,
                    BuildToggle(Settings.MatrixWarnings(m), on => { Settings.FlagBoxWarnings[i] = on; Save(); repaint(); })));

            var rest = Settings.MatrixRest(m);
            var bands = Settings.MatrixGearBands(m);
            var restChoice = BuildSegmented(Contract.FlagBoxRests, PanelMatrix.RestLabels, rest, value =>
            {
                // Through the setter rather than into the array, because the deprecated Gear switch has to move
                // with it or the collapse in Normalise() undoes this on the next load.
                Settings.SetMatrixRest(m, value);
                Save();
                RebuildPage();
            }, PanelKit.SegmentedHeightMatrix);
            var idle = new List<UIElement> { MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.IdleDisplayTitle), PanelMatrix.IdleDisplayTitle, null, null, restChoice) };
            if (PanelMatrix.ShowsGearRows(rest))
            {
                idle.Add(MatrixOption(PanelMatrix.ShiftColoursTitle, null,
                    BuildToggle(bands, on => { Settings.FlagBoxMatrixGearBands[i] = on; Save(); RebuildPage(); })));
            }
            if (PanelMatrix.ShowsCarShiftPoints(rest, bands))
            {
                // Whether the tables are downloaded and have the car in the session, while the switch is on;
                // read on the tick, from properties only, and again when the switch moves. One snapshot of what
                // SimHub last reported: the car's name, its game and its id all come from it, so a car change on
                // the data thread between two reads can never pair one car's name with another's answer.
                var carLine = Ui.Text(string.Empty, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.StatusUpToDate);
                carLine.TextWrapping = TextWrapping.Wrap;
                Action readCar = () =>
                {
                    var live = plugin.Live ?? LiveStatus.None;
                    var known = !string.IsNullOrEmpty(live.CarId) && plugin.CarLights.For(live.CarId) != null;
                    var count = plugin.CarLights.CarCount;
                    var tables = count > 0;
                    var missing = PanelMatrix.TablesMissing(count, plugin.CarLights.Status);
                    var covers = PanelMatrix.TablesCoverGame(live.GameName);
                    var text = PanelMatrix.CarLine(Settings.MatrixGearCarLadder(m), live.CarModel, known, tables, missing, covers);
                    carLine.Text = text ?? string.Empty;
                    carLine.Foreground = Ui.Brush(PanelMatrix.CarLineHex(PanelMatrix.CarLineGood(known, tables)));
                    carLine.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
                };
                readCar();
                OnTick(readCar);
                idle.Add(MatrixOption(PanelMatrix.CarShiftPointsTitle, null,
                    BuildToggle(Settings.MatrixGearCarLadder(m), on => { Settings.FlagBoxMatrixGearCarLadder[i] = on; Save(); readCar(); }), carLine));
            }
            if (PanelMatrix.ShowsRedlineFlash(rest, bands))
            {
                idle.Add(MatrixOption(PanelMatrix.RedlineFlashTitle, null,
                    BuildToggle(Settings.MatrixGearBlink(m), on => { Settings.FlagBoxMatrixGearBlink[i] = on; Save(); })));
            }
            idle.Add(Ui.Soon(MatrixOption(PanelSoon.RpmColourForEverything.Title, null, Ui.Switch(true, null)), PanelSoon.RpmColourForEverything));
            var idleLayer = MatrixLayer(idle.ToArray());

            var device = MatrixLayer(Ui.Soon(
                MatrixLayerHead(null, PanelSoon.SimHubDevice.Title, null, null, Ui.Button(PanelMatrix.SimHubDeviceButton, PanelButtonKind.Outline, PanelButtonSize.Small)),
                PanelSoon.SimHubDevice));

            return Ui.Anchor(Ui.VStack(0,
                head,
                Ui.Anchor(flags, PanelMatrix.AnchorFlags),
                Ui.Anchor(pit, PanelMatrix.AnchorPitLane),
                Ui.Anchor(spotter, PanelMatrix.AnchorSpotter),
                Ui.Anchor(warnings, PanelMatrix.AnchorWarnings),
                Ui.Anchor(idleLayer, PanelMatrix.AnchorIdleDisplay),
                device), PanelMatrix.AnchorPriority);
        }

        /// <summary>The artboard's .layer: a rule over a layer's line and the options under it.</summary>
        private static Border MatrixLayer(params UIElement[] rows)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            foreach (var row in rows)
            {
                if (row != null) stack.Children.Add(row);
            }
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Child = stack,
            };
        }

        /// <summary>
        /// The artboard's .lh: the layer's number in the display family, its name at 15, an optional caption,
        /// an optional link and the control on the right. An empty <paramref name="rank"/> is an unranked
        /// layer, drawn with the artboard's dot in the rank column; a null one drops the column and indents the
        /// name to the options' line, as the SimHub device row is drawn. The row carries its parts, so Ui.Soon
        /// appends its tag after the name.
        /// </summary>
        private static Border MatrixLayerHead(string rank, string title, string caption, FrameworkElement link, FrameworkElement control)
        {
            var titleLine = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var name = Ui.Text(title, PanelShell.RowTitleSize, FontWeights.Medium, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
            titleLine.Children.Add(name);
            var words = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(titleLine);
            if (!string.IsNullOrEmpty(caption))
            {
                var line = Ui.Text(caption, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.TextSecondary);
                line.TextWrapping = TextWrapping.Wrap;
                line.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0);
                words.Children.Add(line);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // The artboard's '·' for an unranked layer, drawn rather than typed so no text box holds a lone glyph.
            if (rank == string.Empty)
            {
                var dot = new Ellipse
                {
                    Width = PanelMatrix.UnrankedDotSize,
                    Height = PanelMatrix.UnrankedDotSize,
                    Fill = Ui.Brush(Theme.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var cell = new Border { Width = PanelMatrix.RankWidth, Margin = new Thickness(0, 0, PanelMatrix.LayerGap, 0), Child = dot };
                grid.Children.Add(cell);
            }
            else if (rank != null)
            {
                var number = Ui.Text(rank, PanelMatrix.RankSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                number.Width = PanelMatrix.RankWidth;
                number.VerticalAlignment = VerticalAlignment.Center;
                number.Margin = new Thickness(0, 0, PanelMatrix.LayerGap, 0);
                grid.Children.Add(number);
            }
            else
            {
                words.Margin = new Thickness(PanelMatrix.OptionIndent, 0, 0, 0);
            }
            Grid.SetColumn(words, 1);
            grid.Children.Add(words);
            if (link != null)
            {
                link.VerticalAlignment = VerticalAlignment.Center;
                link.Margin = new Thickness(PanelMatrix.LayerGap, 0, PanelMatrix.ThresholdsGap, 0);
                Grid.SetColumn(link, 2);
                grid.Children.Add(link);
            }
            if (control != null)
            {
                // A switch has no words of its own, so it is named for a screen reader by its row, as the
                // artboard's aria-labels name them. A press keeps its own words: the greyed device press is
                // heard as "Choose a device", what it shows.
                if (control is ToggleButton) AutomationProperties.SetName(control, title);
                control.VerticalAlignment = VerticalAlignment.Center;
                control.HorizontalAlignment = HorizontalAlignment.Right;
                control.Margin = new Thickness(PanelMatrix.LayerGap, 0, 0, 0);
                Grid.SetColumn(control, 3);
                grid.Children.Add(control);
            }
            return new Border
            {
                Padding = new Thickness(0, PanelMatrix.LayerPaddingY, 0, PanelMatrix.LayerPaddingY),
                Child = grid,
                Tag = new RowParts(titleLine, control),
            };
        }

        /// <summary>
        /// The artboard's .opt: an option of the layer above, indented to its layer's name and padded 7, its words at 14 with
        /// an optional line under them, and the control 16 from them on the right. Carries its parts, so
        /// Ui.Soon appends its tag after the words.
        /// </summary>
        private static Border MatrixOption(string title, string caption, FrameworkElement control, TextBlock line = null)
        {
            var titleLine = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var name = Ui.Text(title, PanelMatrix.OptionTextSize, FontWeights.Normal, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
            titleLine.Children.Add(name);
            var words = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(titleLine);
            if (!string.IsNullOrEmpty(caption))
            {
                var under = Ui.Text(caption, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.TextSecondary);
                under.TextWrapping = TextWrapping.Wrap;
                under.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0);
                words.Children.Add(under);
            }
            if (line != null)
            {
                line.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0);
                words.Children.Add(line);
            }
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(words);
            if (control != null)
            {
                // A switch named by its row, as a layer's is.
                if (control is ToggleButton) AutomationProperties.SetName(control, title);
                control.VerticalAlignment = VerticalAlignment.Center;
                control.HorizontalAlignment = HorizontalAlignment.Right;
                control.Margin = new Thickness(PanelMatrix.OptionGap, 0, 0, 0);
                Grid.SetColumn(control, 1);
                grid.Children.Add(control);
            }
            return new Border
            {
                Padding = new Thickness(PanelMatrix.OptionIndent, PanelMatrix.OptionPaddingY, 0, PanelMatrix.OptionPaddingY),
                Child = grid,
                Tag = new RowParts(titleLine, control),
            };
        }

        /// <summary>Naming a new matrix, in the sheet: which content it will be and the step on the device.</summary>
        private void ShowAddMatrix()
        {
            var slot = Settings.FreeMatrixSlot();
            if (slot == 0) return;
            var name = Ui.Input(PanelMatrix.DefaultName(slot), PanelMatrix.NameWidth);
            var add = Ui.Button(PanelMatrix.AddPanel, PanelButtonKind.Primary, PanelButtonSize.Large);
            // The sheet opens inside the press that asks for it, over the page, so the second press of a
            // double-click on that press can land on this one before the sheet has been read: it is ignored.
            add.PreviewMouseLeftButtonDown += (sender, args) => { if (args.ClickCount > 1) args.Handled = true; };
            add.Click += (sender, args) =>
            {
                matrixSheetPressed = true;
                var added = Settings.AddMatrixPanel(name.Text);
                Save();
                if (added != 0) Select(PanelPage.Matrix, PanelMatrix.SlotId(added));
                Redraw();
                if (added == 0) return;
                // Nothing was installed here: one profile paints every matrix and is installed once, so the
                // line names that profile and the steps left on the device. The redraw has just asked.
                var state = PanelMatrix.StateOf(MatrixPlan());
                Say(new PanelMessage(
                    PanelMatrix.PanelAdded(PanelMatrix.NameOf(Settings.MatrixName(added), added), added, FlagBoxName(), state),
                    PanelMatrix.NeedsInstall(state) ? PanelTone.Caution : PanelTone.Info));
            };
            var cancel = Ui.Button(PanelMatrix.Cancel, PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            var body = Ui.VStack(PanelMatrix.SheetGap,
                Ui.Caption(PanelMatrix.AddPanelCaption(slot, FlagBoxName(), PanelMatrix.StateOf(MatrixPlan()))),
                Ui.SettingRow(PanelMatrix.NameTitle, name, PanelMatrix.NameCaption));
            ShowSheet(PanelMatrix.AddPanel, body, SheetFooter(null, cancel, add));
        }

        private void ShowRenameMatrix(int matrix)
        {
            var current = PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix);
            var name = Ui.Input(current, PanelMatrix.NameWidth);
            var save = Ui.Button(PanelMatrix.Rename, PanelButtonKind.Primary, PanelButtonSize.Large);
            // A blank name is ignored by the settings, so the press waits for one.
            name.TextChanged += (sender, args) => save.IsEnabled = PanelMatrix.CanRename(name.Text);
            save.PreviewMouseLeftButtonDown += (sender, args) => { if (args.ClickCount > 1) args.Handled = true; };
            save.Click += (sender, args) =>
            {
                matrixSheetPressed = true;
                var before = PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix);
                Settings.RenameMatrixPanel(matrix, name.Text);
                Save();
                Redraw();
                var said = PanelMatrix.RenameSaid(before, PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix));
                if (said != null) Say(said);
            };
            var cancel = Ui.Button(PanelMatrix.Cancel, PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            ShowSheet(PanelMatrix.RenameTitle(current),
                Ui.SettingRow(PanelMatrix.NameTitle, name, PanelMatrix.NameCaption),
                SheetFooter(null, cancel, save));
        }

        /// <summary>Removing asks once, in a sheet that says what goes with it.</summary>
        private void ShowRemoveMatrix(int matrix)
        {
            var name = PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix);
            var remove = Ui.Button(PanelMatrix.RemoveConfirm, PanelButtonKind.Danger, PanelButtonSize.Large);
            remove.PreviewMouseLeftButtonDown += (sender, args) => { if (args.ClickCount > 1) args.Handled = true; };
            remove.Click += (sender, args) =>
            {
                matrixSheetPressed = true;
                Settings.RemoveMatrixPanel(matrix);
                Save();
                Select(PanelPage.Matrix, null);
                Redraw();
                Say(PanelMatrix.Removed(name, matrix));
            };
            var cancel = Ui.Button(PanelMatrix.Cancel, PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            ShowSheet(PanelMatrix.RemoveTitle(name), Ui.Caption(PanelMatrix.RemoveCaption(name)), SheetFooter(null, cancel, remove));
        }
    }
}
