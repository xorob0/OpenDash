// SettingsControl.Matrix.cs: the Matrix page, as Matrix.dc.html draws it -- the flag box profile on the
// title's line, a card for each matrix, and for the one selected its preview beside what may take it over and
// what it shows at rest.
//
// Every word, number and decision is PanelMatrix's; this file only draws. The profile's state is asked of
// SimHub when the page is opened and again after each press that redraws it, and only a press writes it (ADR
// 0013). The build reads SimHub's matrix profiles, so it
// never asks to be rebuilt by a wheel's lighting press: every picture re-dims in place through OnLighting.
// docs/design/flag-box.md is what the box draws.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The chip the preview is on, kept for the session across builds and matrices.</summary>
        private string matrixPreviewScenario = PanelMatrix.IdleScenario;

        /// <summary>
        /// What SimHub holds for the flag box, asked once and kept while the page is open: asking parses the
        /// whole embedded profile, and a card, a side or Shift colours rebuilds the page. It is let go of when
        /// the page is left and before each press that redraws it, so each of those asks again.
        /// </summary>
        private FlagBoxPlan matrixPlan;

        private FlagBoxPlan MatrixPlan()
        {
            if (plugin.FlagBoxJson == null) return null;
            if (matrixPlan == null) matrixPlan = SafePlan();
            return matrixPlan;
        }

        private FrameworkElement BuildMatrixPage(PanelRoute to)
        {
            // No DrawsLighting(): the build reads the flag box plan from SimHub (SafePlan).
            OnLeave("Matrix.plan", () => matrixPlan = null);
            var plan = MatrixPlan();
            var panels = Settings.MatrixPanels().ToList();
            var slot = PanelMatrix.SelectedSlot(panels, Selected(PanelPage.Matrix));
            Border selectedCard = null;
            var cards = BuildMatrixCards(panels, slot, picture => selectedCard = picture);
            return PageLayout(PanelMatrix.Title,
                Ui.Anchor(BuildMatrixProfile(plan), PanelMatrix.AnchorProfile),
                // The by-hand import, only when SimHub's matrix settings could not be reached.
                PanelMatrix.ShowsImportFallback(PanelMatrix.StateOf(plan)) ? BuildFlagBoxImportFallback(plan) : null,
                Ui.Anchor(cards, PanelMatrix.AnchorPanels),
                slot == 0 ? null : BuildMatrixSelected(slot, selectedCard));
        }

        /// <summary>
        /// The flag box profile on the title's line: a dot in its state's ink, the profile as SimHub lists it
        /// with its version, and the one press that changes it, which is left out when there is nothing to
        /// install or nowhere to put it.
        /// </summary>
        private FrameworkElement BuildMatrixProfile(FlagBoxPlan plan)
        {
            var state = PanelMatrix.StateOf(plan);
            var version = plan == null ? null : plan.InstalledVersion;
            var dot = new Ellipse
            {
                Width = PanelMatrix.ProfileDotSize,
                Height = PanelMatrix.ProfileDotSize,
                Fill = Ui.Brush(PanelLightRows.DotHex(state)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var line = Ui.Text(PanelMatrix.ProfileLine(FlagBoxName(), state, version), Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
            line.VerticalAlignment = VerticalAlignment.Center;
            var row = Ui.HStack(PanelMatrix.ProfileGap, dot, line);
            if (PanelMatrix.ProfileHasButton(state))
            {
                var action = PanelMatrix.ProfileRow(state, version);
                var primary = action.Style == PanelButton.Primary;
                var button = Ui.Button(action.Button, primary ? PanelButtonKind.Primary : PanelButtonKind.Ghost, PanelButtonSize.Small);
                if (!primary) button.Padding = new Thickness(PanelMatrix.ProfileButtonPaddingX, 0, PanelMatrix.ProfileButtonPaddingX, 0);
                button.ToolTip = PanelMatrix.ProfileTooltip(state);
                button.Click += (sender, args) =>
                {
                    var result = InstallFlagBox();
                    // Asked again on the redraw, unless it failed, which asking again cannot see.
                    matrixPlan = PanelMatrix.KeepsPressResult(result.State) ? result : null;
                    // Redraw asks what needs fixing again, so the sidebar's dot and Home move with it.
                    Redraw();
                    var said = PanelMatrix.InstallSaid(state, result.State, FlagBoxName());
                    if (said != null) Say(said);
                    if (!string.IsNullOrEmpty(result.Note)) Say(PanelMessage.Info(result.Note));
                };
                row.Children.Add(button);
            }
            row.ToolTip = FlagBoxInstallPlan.Summary(plan ?? new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded }, plugin.FlagBox?.Path);
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
                var picture = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, PanelMatrix.OptionsFor(Settings, m)), MatrixStyle.Card, MatrixDim());
                OnLighting(() => Ui.Redim(picture, MatrixDim()));
                if (m == selected) selectedPicture(picture);
                cards.Add(Ui.MatrixCard(picture, PanelMatrix.NameOf(Settings.MatrixName(m), m), PanelMatrix.CardLine(m, Settings.MatrixSide(m), shown),
                    PanelMatrix.CardLineHex(shown), m == selected, () =>
                    {
                        Select(PanelPage.Matrix, PanelMatrix.SlotId(m));
                        RebuildPage();
                    }));
            }
            var add = Ui.InlineAddCard(PanelMatrix.AddPanel, PanelKit.MatrixAddIcon, ShowAddMatrix, PanelMatrix.AddCount(panels.Count));
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
            cards.Add(add);
            var grid = Ui.CardGrid(PanelMatrix.CardMinWidth, PanelMatrix.CardGap, PanelMatrix.CardColumns, cards.ToArray());
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
            // A WrapPanel, not a horizontal StackPanel, so the name is measured at the column's width and
            // trims, and the content number wraps under a long name rather than being cut off.
            var heading = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            heading.Children.Add(title);
            var slotCaption = PanelMatrix.SlotCaption(name, m);
            if (slotCaption != null)
            {
                var slot = Ui.Text(slotCaption, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
                slot.VerticalAlignment = VerticalAlignment.Bottom;
                slot.Margin = new Thickness(PanelMatrix.HeaderGap, 0, 0, PanelMatrix.SlotCaptionLift);
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
                check.Click += (sender, args) =>
                {
                    matrixPlan = null;
                    CheckAgain();
                };
                var fix = Ui.FixBox(PanelMatrix.FixTitle(m), null, PanelMatrix.FixSteps(m, FlagBoxName()), check);
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
        /// The 8x8 at the size of the real thing's cells, what it shows under the chip chosen, the link to
        /// every device at once, and the chips. A chip repaints the picture and the chips in place.
        /// </summary>
        private MatrixPreviewColumn BuildMatrixPreview(int matrix, Border cardPicture)
        {
            var m = matrix;
            var preview = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), PanelMatrix.OptionsFor(Settings, m)), MatrixStyle.Preview, MatrixDim());
            OnLighting(() => Ui.Redim(preview, MatrixDim()));
            preview.HorizontalAlignment = HorizontalAlignment.Center;
            var frame = new Border
            {
                Padding = new Thickness(PanelMatrix.PreviewFramePadding),
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Child = preview,
            };

            var all = Ui.LinkButton(PanelMatrix.AllDevices);
            all.FontSize = Theme.SizeSmall;
            all.Height = double.NaN;
            all.HorizontalAlignment = HorizontalAlignment.Left;
            all.Click += (sender, args) => Open(PanelPage.Rig, PanelMatrix.PreviewScenario(matrixPreviewScenario));
            // New in this release (the Map's "Live 8×8 preview"). The preview has no title to follow, and the
            // frame's padding is narrower than the tag is tall, so the tag follows the line under the frame.
            var links = Ui.HStack(PanelMatrix.NewTagGap, all, Ui.NewTag());
            links.HorizontalAlignment = HorizontalAlignment.Left;

            var chips = new WrapPanel { Orientation = Orientation.Horizontal };
            AutomationProperties.SetName(chips, PanelMatrix.PreviewChipsName);
            Action drawChips = null;
            Action repaint = () =>
            {
                var options = PanelMatrix.OptionsFor(Settings, m);
                MatrixRepaint(preview, PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), options), MatrixStyle.Preview);
                if (cardPicture != null) MatrixRepaint(cardPicture, PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, options), MatrixStyle.Card);
            };
            drawChips = () =>
            {
                var scenario = PanelMatrix.PreviewScenario(matrixPreviewScenario);
                var focused = -1;
                for (var i = 0; i < chips.Children.Count; i++)
                {
                    if (chips.Children[i].IsKeyboardFocusWithin) focused = i;
                }
                chips.Children.Clear();
                foreach (var id in PanelMatrix.PreviewScenarios)
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

            var column = Ui.VStack(PanelMatrix.PreviewGap, frame, links, chips);
            return new MatrixPreviewColumn(Ui.Anchor(column, PanelMatrix.AnchorPreview), repaint);
        }

        /// <summary>The scenario the preview draws under the chip it is on, as the matrix's settings leave it.</summary>
        private string MatrixDrawn(int matrix)
        {
            return PanelMatrix.DrawnScenario(PanelMatrix.PreviewScenario(matrixPreviewScenario), PanelMatrix.OptionsFor(Settings, matrix), Settings.MatrixCriticalOnly(matrix));
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
            var i = m - 1;

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

            var side = BuildSegmented(Contract.FlagBoxSides, PanelMatrix.SideLabels, Settings.MatrixSide(m), value =>
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
                // Whether the car in the session is in the tables, while the switch is on; read on the tick,
                // from properties only, and again when the switch moves.
                var carLine = Ui.Text(string.Empty, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.StatusUpToDate);
                carLine.TextWrapping = TextWrapping.Wrap;
                Action readCar = () =>
                {
                    var live = plugin.Live ?? LiveStatus.None;
                    var known = plugin.LiveCarHasTable;
                    var text = PanelMatrix.CarLine(Settings.MatrixGearCarLadder(m), live.CarModel, known);
                    carLine.Text = text ?? string.Empty;
                    carLine.Foreground = Ui.Brush(PanelMatrix.CarLineHex(known));
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
            idle.Add(Ui.Soon(MatrixOption(PanelSoon.RpmColourForEverything.Title, null, Ui.Switch(false, null)), PanelSoon.RpmColourForEverything));
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
            if (rank == string.Empty)
            {
                // The artboard's '·', drawn rather than typed so no text box holds a lone glyph.
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
            add.Click += (sender, args) =>
            {
                var added = Settings.AddMatrixPanel(name.Text);
                Save();
                if (added != 0) Select(PanelPage.Matrix, PanelMatrix.SlotId(added));
                matrixPlan = null;
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
            save.Click += (sender, args) =>
            {
                var before = PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix);
                Settings.RenameMatrixPanel(matrix, name.Text);
                Save();
                matrixPlan = null;
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
            remove.Click += (sender, args) =>
            {
                Settings.RemoveMatrixPanel(matrix);
                Save();
                Select(PanelPage.Matrix, null);
                matrixPlan = null;
                Redraw();
                Say(PanelMatrix.Removed(name, matrix));
            };
            var cancel = Ui.Button(PanelMatrix.Cancel, PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            ShowSheet(PanelMatrix.RemoveTitle(name), Ui.Caption(PanelMatrix.RemoveCaption), SheetFooter(null, cancel, remove));
        }
    }
}
