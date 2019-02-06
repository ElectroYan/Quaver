/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 * Copyright (c) Swan & The Quaver Team <support@quavergame.com>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Quaver.API.Enums;
using Quaver.API.Maps.Structures;
using Quaver.Shared.Audio;
using Quaver.Shared.Config;
using Quaver.Shared.Graphics;
using Quaver.Shared.Graphics.Notifications;
using Quaver.Shared.Screens.Editor.Actions;
using Quaver.Shared.Screens.Editor.Actions.Rulesets;
using Quaver.Shared.Screens.Editor.Actions.Rulesets.Keys;
using Quaver.Shared.Screens.Editor.UI.Graphing;
using Quaver.Shared.Screens.Editor.UI.Rulesets.Keys.Components;
using Quaver.Shared.Screens.Editor.UI.Rulesets.Keys.Scrolling;
using Quaver.Shared.Screens.Editor.UI.Rulesets.Keys.Scrolling.HitObjects;
using Quaver.Shared.Screens.Editor.UI.Rulesets.Keys.Scrolling.Timeline;
using Quaver.Shared.Screens.Editor.UI.Toolkit;
using Quaver.Shared.Screens.Gameplay.Rulesets.Keys;
using Quaver.Shared.Skinning;
using Wobble.Bindables;
using Wobble.Graphics;
using Wobble.Input;
using Wobble.Window;
using Keys = Microsoft.Xna.Framework;

namespace Quaver.Shared.Screens.Editor.UI.Rulesets.Keys
{
    public class EditorRulesetKeys : EditorRuleset
    {
        /// <summary>
        ///     Used for scrolling hitobjects & timing lines.
        /// </summary>
        public EditorScrollContainerKeys ScrollContainer { get; private set; }

        /// <summary>
        ///     The selected tool the user has when compositing the map
        /// </summary>
        public Bindable<EditorCompositionTool> CompositionTool { get; }

        /// <summary>
        ///     Keeps track if we're currently pending a long note release specification for that
        ///     given lane.
        /// </summary>
        public List<HitObjectInfo> PendingLongNoteReleases { get; } = new List<HitObjectInfo>(new HitObjectInfo[7]);

        /// <summary>
        /// </summary>
        private EditorScreenView View => Screen.View as EditorScreenView;

        /// <summary>
        /// </summary>
        public Bindable<EditorVisualizationGraphType> SelectedVisualizationGraph { get; }

        /// <summary>
        /// </summary>
        public Dictionary<EditorVisualizationGraphType, EditorVisualizationGraphContainer> VisualizationGraphs { get; }
            = new Dictionary<EditorVisualizationGraphType, EditorVisualizationGraphContainer>();

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="screen"></param>
        public EditorRulesetKeys(EditorScreen screen) : base(screen)
        {
            CompositionTool = new Bindable<EditorCompositionTool>(EditorCompositionTool.Select) { Value = EditorCompositionTool.Select };
            SelectedVisualizationGraph = new Bindable<EditorVisualizationGraphType>(EditorVisualizationGraphType.Tick)
                { Value = EditorVisualizationGraphType.Tick};

            CreateScrollContainer();
            CreateVisualizationGraphs();
            ActionManager = CreateActionManager();
            SkinManager.SkinLoaded += OnSkinLoaded;
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            VisualizationGraphs[SelectedVisualizationGraph.Value]?.Update(gameTime);
            base.Update(gameTime);
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            foreach (var i in VisualizationGraphs)
            {
                if (i.Value?.Graph != null)
                    i.Value.Graph.Visible = i.Value.Type == SelectedVisualizationGraph.Value;
            }

            VisualizationGraphs[SelectedVisualizationGraph.Value]?.Draw(gameTime);
            base.Draw(gameTime);
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public override void Destroy()
        {
            SkinManager.SkinLoaded -= OnSkinLoaded;

            foreach (var i in VisualizationGraphs)
                i.Value.Dispose();

            base.Destroy();
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void HandleInput(GameTime gameTime)
        {
            if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.PageUp))
                ConfigManager.EditorScrollSpeedKeys.Value++;

            if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.PageDown))
                ConfigManager.EditorScrollSpeedKeys.Value--;

            if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.Delete))
                DeleteSelectedHitObjects();

            // Clever way of handing key input with num keys since the enum values are 1 after each other.
            for (var i = 0; i < WorkingMap.GetKeyCount(); i++)
            {
                if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.D1 + i))
                    PlaceObject(CompositionInputDevice.Keyboard, i + 1, AudioEngine.Track.Time);
            }

            // Change between composition tools (only when shift isn't held down).
            // if shift is held down, then it'll change the beat snap.
            if (KeyboardManager.CurrentState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                && KeyboardManager.CurrentState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.RightShift))
            {
                var index = (int) CompositionTool.Value;

                if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.Up))
                {
                    if (index - 1 >= 0)
                        CompositionTool.Value = (EditorCompositionTool) index - 1;
                }

                if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.Down))
                {
                    if (index + 1 < Enum.GetNames(typeof(EditorCompositionTool)).Length)
                        CompositionTool.Value = (EditorCompositionTool) index + 1;
                }

                SwitchGraphs();
            }

            switch (CompositionTool.Value)
            {
                case EditorCompositionTool.Select:
                    HandleHitObjectSelection();
                    break;
                case EditorCompositionTool.Note:
                case EditorCompositionTool.LongNote:
                case EditorCompositionTool.Mine:
                    HandleHitObjectMouseInput();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// </summary>
        private void SwitchGraphs()
        {
            if (KeyboardManager.CurrentState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
                KeyboardManager.CurrentState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl))
                return;

            var index = (int) SelectedVisualizationGraph.Value;

            if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.Z))
            {
                if (index - 1 >= 0)
                    SelectedVisualizationGraph.Value = (EditorVisualizationGraphType) index - 1;
            }

            if (KeyboardManager.IsUniqueKeyPress(Microsoft.Xna.Framework.Input.Keys.X))
            {
                if (index + 1 < Enum.GetNames(typeof(EditorVisualizationGraphType)).Length)
                    SelectedVisualizationGraph.Value = (EditorVisualizationGraphType) index + 1;
            }
        }

        /// <summary>
        ///     Toggles the scroll direction for the editor.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void ToggleScrollDirection()
        {
            switch (Screen.Ruleset.WorkingMap.Mode)
            {
                case GameMode.Keys4:
                    ConfigManager.ScrollDirection4K.Value = ConfigManager.ScrollDirection4K.Value != ScrollDirection.Down
                        ? ScrollDirection.Down : ScrollDirection.Up;
                    break;
                case GameMode.Keys7:
                    ConfigManager.ScrollDirection7K.Value = ConfigManager.ScrollDirection7K.Value != ScrollDirection.Down
                        ? ScrollDirection.Down : ScrollDirection.Up;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Change hit pos line Y
            switch (GameplayRulesetKeys.ScrollDirection)
            {
                case ScrollDirection.Split:
                case ScrollDirection.Down:
                    ScrollContainer.HitPositionLine.Y = ScrollContainer.HitPositionY;
                    break;
                case ScrollDirection.Up:
                    ScrollContainer.HitPositionLine.Y = (int) WindowManager.Height - ScrollContainer.HitPositionY;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        ///     Handles performing actions w/ the mouse
        /// </summary>
        private void HandleHitObjectMouseInput()
        {
            // Prevent clicking if in range of the nav/control bars.
            if (View.NavigationBar.ScreenRectangle.Contains(MouseManager.CurrentState.Position) ||
                View.ControlBar.ScreenRectangle.Contains(MouseManager.CurrentState.Position))
                return;

            // Left click/place object
            if (MouseManager.IsUniqueClick(MouseButton.Left))
            {
                var lane = ScrollContainer.GetLaneFromX(MouseManager.CurrentState.X);

                if (lane == -1)
                    return;

                var time = (int) ScrollContainer.GetTimeFromY(MouseManager.CurrentState.Y) / ScrollContainer.TrackSpeed;
                var timeFwd = (int) AudioEngine.GetNearestSnapTimeFromTime(WorkingMap, Direction.Forward, Screen.BeatSnap.Value, time);
                var timeBwd = (int) AudioEngine.GetNearestSnapTimeFromTime(WorkingMap, Direction.Backward, Screen.BeatSnap.Value, time);

                var fwdDiff = Math.Abs(time - timeFwd);
                var bwdDiff = Math.Abs(time - timeBwd);

                if (fwdDiff < bwdDiff)
                    time = timeFwd;
                else if (bwdDiff < fwdDiff)
                    time = timeBwd;

                PlaceObject(CompositionInputDevice.Mouse, lane, time);
            }

            // Right click/delete object.
            if (MouseManager.IsUniqueClick(MouseButton.Right))
                DeleteHoveredHitObject();
        }

        /// <summary>
        ///     Handles the selecting of hitobjects
        /// </summary>
        private void HandleHitObjectSelection()
        {
            if (!MouseManager.IsUniqueClick(MouseButton.Left))
                return;

            var hoveredObject = ScrollContainer.GetHoveredHitObject();

            // User has clicked on a new object and wants to select it.
            if (hoveredObject != null)
            {
                // Object isn't hovered, so we need to add it
                if (!SelectedHitObjects.Contains(hoveredObject.Info))
                    SelectHitObject(hoveredObject);

                // In the event that the user isn't pressing control, deselect all other hitobjects
                if (KeyboardManager.CurrentState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
                    KeyboardManager.CurrentState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl))
                    return;

                for (var i = SelectedHitObjects.Count - 1; i >= 0; i--)
                {
                    if (SelectedHitObjects[i] != hoveredObject.Info)
                        DeselectHitObject(ScrollContainer.HitObjects.Find(y => y.Info == SelectedHitObjects[i]));
                }

                return;
            }

            for (var i = SelectedHitObjects.Count - 1; i >= 0; i--)
                DeselectHitObject(ScrollContainer.HitObjects.Find(y => y.Info == SelectedHitObjects[i]));
        }

        /// <summary>
        ///     Places a HitObject at a given lane.
        /// </summary>
        /// <param name="inputDevice"></param>
        /// <param name="lane"></param>
        /// <param name="time"></param>
        private void PlaceObject(CompositionInputDevice inputDevice, int lane, double time)
        {
            var am = ActionManager as EditorActionManagerKeys;

            if (HandlePendingLongNoteReleases(lane, time))
                return;

            // Find an existing object in the current lane at the same time, so we can determine if
            // the object should be placed or deleted accordingly.
            HitObjectInfo existingObject = null;
            DrawableEditorHitObject hoveredObject = null;

            switch (inputDevice)
            {
                case CompositionInputDevice.Keyboard:
                    existingObject = WorkingMap.HitObjects.Find(x => x.StartTime == (int) time && x.Lane == lane);
                    break;
                case CompositionInputDevice.Mouse:
                    hoveredObject = ScrollContainer.GetHoveredHitObject();

                    if (hoveredObject != null)
                        existingObject = hoveredObject.Info;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inputDevice), inputDevice, null);
            }

            // There's no object currently at this position, so add it.
            if (existingObject == null)
            {
                switch (CompositionTool.Value)
                {
                    case EditorCompositionTool.Note:
                        am?.PlaceHitObject(lane, time);
                        break;
                    case EditorCompositionTool.LongNote:
                        am?.PlaceLongNote(lane, time);

                        // Makes sure the long note is marked as pending, so any future objects placed in this lane
                        // will be awarded to this LN's end.
                        var workingObject = WorkingMap.HitObjects.Find(x => x.StartTime == (int) time && x.Lane == lane);
                        PendingLongNoteReleases[lane - 1] = workingObject;

                        // Make the long note appear as inactive/dead. Gives a visual effect to the user that
                        // they need to do something with the note.
                        if (!(ScrollContainer.HitObjects.Find(x => x.Info == workingObject) is DrawableEditorHitObjectLong drawable))
                            return;

                        drawable.AppearAsInactive();

                        NotificationManager.Show(NotificationLevel.Info, "Scroll through the timeline and place the end of the long note.");
                        break;
                    default:
                        NotificationManager.Show(NotificationLevel.Error, "This tool isn't implemented yet. Choose another!");
                        break;
                }
            }
            // An object exists, so delete it.
            else
            {
                switch (inputDevice)
                {
                    case CompositionInputDevice.Keyboard:
                        am?.DeleteHitObject(existingObject);
                        break;
                    case CompositionInputDevice.Mouse:
                        // hoveredObject?.AppearAsSelected();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(inputDevice), inputDevice, null);
                }
            }
        }

        /// <summary>
        ///     Selects an individual HitObject
        /// </summary>
        /// <param name="h"></param>
        private void SelectHitObject(DrawableEditorHitObject h)
        {
            h.AppearAsSelected();
            SelectedHitObjects.Add(h.Info);
        }

        /// <summary>
        ///     Deselects an individual HitObject
        /// </summary>
        /// <param name="h"></param>
        private void DeselectHitObject(DrawableEditorHitObject h)
        {
            var layer = View.LayerCompositor.ScrollContainer.AvailableItems[h.Info.EditorLayer];

            if (PendingLongNoteReleases.Contains(h.Info))
            {
                var ln = h as DrawableEditorHitObjectLong;
                ln?.AppearAsInactive();
            }
            else if (layer.Hidden)
                h.AppearAsHiddenInLayer();
            else
                h.AppearAsActive();

            SelectedHitObjects.Remove(h.Info);
        }

        /// <summary>
        ///     Deletes all hitobjects that are currently selected.
        /// </summary>
        private void DeleteSelectedHitObjects()
        {
            ActionManager.Perform(new EditorActionBatchDeleteHitObjectKeys(this, ScrollContainer, new List<HitObjectInfo>(SelectedHitObjects)));
            SelectedHitObjects.Clear();
        }

        /// <summary>
        ///     Deletes the HitObject that is currently hovered over.
        /// </summary>
        private void DeleteHoveredHitObject()
        {
            var obj = ScrollContainer.GetHoveredHitObject();

            if (obj == null)
                return;

            var am = ActionManager as EditorActionManagerKeys;

            PendingLongNoteReleases[obj.Info.Lane - 1] = null;
            am?.DeleteHitObject(obj.Info);
        }

        /// <summary>
        ///     Handles any long note releases that are currently pending.
        ///     If returned false, nothing has been handled/needs to be handled.
        /// </summary>
        /// <param name="lane"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        private bool HandlePendingLongNoteReleases(int lane, double time)
        {
            // User has a pending long note release for this lane, so that needs to be taken care of.
            if (PendingLongNoteReleases[lane - 1] == null)
                return false;

            var pendingObject = PendingLongNoteReleases[lane - 1];

            if ((int) time < pendingObject.StartTime)
            {
                NotificationManager.Show(NotificationLevel.Error, "You need to select a position later than the start time");
                return true;
            }

            // Returning false here because the user should be able to delete the note if they're
            // still on the starting area.
            if ((int) time == pendingObject.StartTime)
            {
                PendingLongNoteReleases[lane - 1] = null;
                return false;
            }

            // Long note is no longer pending given that the user has entered a correct position.
            PendingLongNoteReleases[lane - 1] = null;

            // Resize the long note and then reset the color of it.
            pendingObject.EndTime = (int) time;

            if (View.LayerCompositor.ScrollContainer.AvailableItems[pendingObject.EditorLayer].Hidden)
                ScrollContainer.ResizeLongNote(pendingObject).AppearAsHiddenInLayer();
            else
                ScrollContainer.ResizeLongNote(pendingObject).AppearAsActive();

            return true;
        }

        /// <summary>
        ///     Called when the user's skin has loaded (from the options menu),
        ///     so we can do a reload of it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSkinLoaded(object sender, SkinReloadedEventArgs e)
        {
            ScrollContainer.Destroy();
            ScrollContainer = new EditorScrollContainerKeys(this) { Parent = Container };

            foreach (var i in VisualizationGraphs)
                i.Value.SetGraphXPos();
        }

        /// <summary>
        /// </summary>
        public void CreateScrollContainer()
        {
            ScrollContainer = new EditorScrollContainerKeys(this) {Parent = Container};

            foreach (var i in VisualizationGraphs)
                i.Value?.SetGraphXPos();
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <returns></returns>
        protected sealed override EditorActionManager CreateActionManager() => new EditorActionManagerKeys(this);

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <returns></returns>
        protected override List<EditorCompositionToolButton> CreateCompositionToolButtons() => new List<EditorCompositionToolButton>
        {
            new EditorCompositionToolButton(EditorCompositionTool.Select),
            new EditorCompositionToolButton(EditorCompositionTool.Note),
            new EditorCompositionToolButton(EditorCompositionTool.LongNote),
            new EditorCompositionToolButton(EditorCompositionTool.Mine)
        };

        /// <summary>
        /// </summary>
        private void CreateVisualizationGraphs()
        {
            foreach (EditorVisualizationGraphType type in Enum.GetValues(typeof(EditorVisualizationGraphType)))
                VisualizationGraphs.Add(type, new EditorVisualizationGraphContainer(type, this, WorkingMap));
        }
    }
}
