﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EVEMon.Common;
using EVEMon.Common.Constants;
using EVEMon.Common.Controls;
using EVEMon.Common.CustomEventArgs;
using EVEMon.Common.Data;
using EVEMon.Common.Enumerations;
using EVEMon.Common.Extensions;
using EVEMon.Common.Factories;
using EVEMon.Common.Helpers;
using EVEMon.Common.Interfaces;
using EVEMon.Common.Models;

namespace EVEMon.SkillPlanner
{
    /// <summary>
    /// UserControl to display a tree of masteries.
    /// </summary>
    public sealed partial class MasteryTreeDisplayControl : UserControl
    {
        private const string TrainedIcon = "Trained";
        private const string TrainableIcon = "Trainable";
        private const string UntrainableIcon = "Untrainable";
        private const string SkillBookIcon = "Skillbook";
        private const string PlannedIcon = "Planned";

        // Blank image list for 'Safe for work' setting
        private readonly ImageList m_emptyImageList = new ImageList();

        private Plan m_plan;
        private Character m_character;
        private Font m_boldFont;
        private MasteryShip m_masteryShip;

        private bool m_allExpanded;


        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MasteryTreeDisplayControl"/> class.
        /// </summary>
        public MasteryTreeDisplayControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.ContainerControl |
                     ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();

            InitializeComponent();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Gets or sets the current plan.
        /// </summary>
        [Browsable(false)]
        public Plan Plan
        {
            get { return m_plan; }
            set
            {
                m_plan = value;
                if (m_plan == null)
                    return;

                m_character = (Character)m_plan.Character;
                UpdateTree();
            }
        }

        /// <summary>
        /// Gets or sets the mastery ship.
        /// </summary>
        [Browsable(false)]
        public MasteryShip MasteryShip
        {
            get { return m_masteryShip; }
            set
            {
                if (value == m_masteryShip)
                    return;

                m_masteryShip = value;
                UpdateTree();
            }
        }

        /// <summary>
        /// Gets mastery of the displayed class which contains the current selection.
        /// </summary>
        [Browsable(false)]
        public Mastery SelectedMasteryLevel
        {
            get
            {
                TreeNode node = treeView.SelectedNode;
                while (node != null)
                {
                    Mastery masteryLevel = node.Tag as Mastery;
                    if (masteryLevel != null)
                        return masteryLevel;

                    node = node.Parent;
                }
                return null;
            }
        }
        
        #endregion


        #region Event Handlers

        /// <summary>
        /// On load, complete the initialization.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            // Return on design mode
            if (DesignMode || this.IsDesignModeHosted())
                return;

            treeView.DrawNode += treeView_DrawNode;
            treeView.MouseDown += treeView_MouseDown;

            cmListSkills.Opening += cmListSkills_Opening;

            m_boldFont = FontFactory.GetFont(Font, FontStyle.Bold);
            treeView.Font = FontFactory.GetFont("Microsoft Sans Serif", 8.25F);
            treeView.ItemHeight = (treeView.Font.Height * 2) + 6;

            m_emptyImageList.ImageSize = new Size(30, 24);
            m_emptyImageList.Images.Add(new Bitmap(30, 24));

            EveMonClient.SettingsChanged += EveMonClient_SettingsChanged;
            EveMonClient.CharacterUpdated += EveMonClient_CharacterUpdated;
            EveMonClient.PlanChanged += EveMonClient_PlanChanged;
            Disposed += OnDisposed;
        }

        /// <summary>
        /// Unsubscribe events on disposing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDisposed(object sender, EventArgs e)
        {
            EveMonClient.SettingsChanged -= EveMonClient_SettingsChanged;
            EveMonClient.CharacterUpdated -= EveMonClient_CharacterUpdated;
            EveMonClient.PlanChanged -= EveMonClient_PlanChanged;
            Disposed -= OnDisposed;
        }

        /// <summary>
        /// On settings change, we update the tree.
        /// </summary>
        /// <remarks>Relates to safe for work setting</remarks>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void EveMonClient_SettingsChanged(object sender, EventArgs e)
        {
            UpdateTree();
        }

        /// <summary>
        /// Fired when one of the character changed (skill completion, update from CCP, etc).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EveMonClient_CharacterUpdated(object sender, CharacterChangedEventArgs e)
        {
            if (m_plan == null)
                return;

            if (e.Character != m_plan.Character)
                return;

            UpdateTree();
        }

        /// <summary>
        /// Occurs when the plan changes, we update the tree.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EveMonClient_PlanChanged(object sender, PlanChangedEventArgs e)
        {
            if ((e.Plan != m_plan) || (e.Plan.Character != m_plan.Character))
                return;

            UpdateTree();
        }

        /// <summary>
        /// When the treeview is clicked, we manually select nodes since the bounding boxes are incorrect due to custom draw.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView_MouseDown(object sender, MouseEventArgs e)
        {
            // Perform the selection manually since the bound's width and x are incorrect in owndraw
            TreeNode selection = null;
            for (TreeNode node = treeView.TopNode; node != null; node = node.NextVisibleNode)
            {
                if (node.Bounds.Top > e.Y || node.Bounds.Bottom < e.Y)
                    continue;

                // If the user clicked the "arrow zone", we do not change the selection and just return
                if (e.X < (node.Bounds.Left - 32))
                    return;

                selection = node;
                break;
            }

            treeView.SelectedNode = selection;
        }

        /// <summary>
        /// Forces the selection update when a node is right-clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                treeView.SelectedNode = e.Node;
        }

        /// <summary>
        /// Event handler for treenode double click event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag is Mastery)
            {
                if (e.Node.IsExpanded)
                {
                    e.Node.Collapse();
                    return;
                }

                e.Node.Expand();
            }

            showInBrowserMenu_Click(sender, e);
        }

        #endregion


        #region Tree building

        /// <summary>
        /// Updates the shown treecontrol
        /// </summary>
        private void UpdateTree()
        {
            Mastery oldSelection = SelectedMasteryLevel;
            TreeNode newSelection = null;

            treeView.ImageList = (Settings.UI.SafeForWork ? m_emptyImageList : imageList);

            treeView.BeginUpdate();
            try
            {
                // Clear the old items
                treeView.Nodes.Clear();

                // No update when not fully initialized
                if (m_character == null || m_masteryShip == null)
                    return;

                // Create the nodes when not done, yet
                if (treeView.Nodes.Count == 0)
                {
                    foreach (Mastery masteryLevel in m_masteryShip)
                    {
                        TreeNode node = CreateNode(masteryLevel);
                        treeView.Nodes.Add(node);

                        // Does the old selection still exists ?
                        if (masteryLevel == oldSelection)
                            newSelection = node;
                    }
                }

                // Update the nodes
                foreach (TreeNode node in treeView.Nodes)
                {
                    UpdateNode(node);
                }

                // Is the old selection kept ? Then we select the matching node
                if (newSelection != null)
                    treeView.SelectedNode = newSelection;
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        /// <summary>
        /// Create a node from a mastery.
        /// </summary>
        /// <param name="masteryLevel">The mastery level.</param>
        /// <returns></returns>
        private TreeNode CreateNode(Mastery masteryLevel)
        {
            TreeNode node = new TreeNode
            {
                Text = masteryLevel.ToString(),
                Tag = masteryLevel
            };

            foreach (CertificateLevel certificate in masteryLevel
                .OrderBy(cert => cert.Certificate.Class.Name)
                .Select(cert => cert.ToCharacter(m_character).GetCertificateLevel(masteryLevel.Level)))
            {
                node.Nodes.Add(CreateNode(certificate));
            }

            return node;
        }

        /// <summary>
        /// Create a node from a certificate.
        /// </summary>
        /// <param name="certificateLevel">The certificate level.</param>
        /// <returns></returns>
        private static TreeNode CreateNode(CertificateLevel certificateLevel)
        {
            TreeNode node = new TreeNode
            {
                Text = certificateLevel.Certificate.Class.ToString(),
                Tag = certificateLevel
            };

            // Add this certificate's prerequisites
            foreach (SkillLevel prereqSkill in certificateLevel.PrerequisiteSkills)
            {
                node.Nodes.Add(CreateNode(prereqSkill));
            }

            return node;
        }

        /// <summary>
        /// Create a node from a prerequisite skill.
        /// </summary>
        /// <param name="skillPrereq">The skill prerequesites</param>
        /// <returns></returns>
        private static TreeNode CreateNode(SkillLevel skillPrereq)
        {
            TreeNode node = new TreeNode
            {
                Text = skillPrereq.ToString(),
                Tag = skillPrereq
            };

            // Add this skill's prerequisites
            foreach (SkillLevel prereqSkill in skillPrereq.Skill.Prerequisites
                .Where(prereqSkill => prereqSkill.Skill != skillPrereq.Skill))
            {
                node.Nodes.Add(CreateNode(prereqSkill));
            }

            return node;
        }

        /// <summary>
        /// Updates the specified node and its children.
        /// </summary>
        /// <param name="node">The Treenode</param>
        private void UpdateNode(TreeNode node)
        {
            Mastery masteryLevel = node.Tag as Mastery;
            CertificateLevel certLevel = node.Tag as CertificateLevel;

            // The node represents a mastery level
            if (masteryLevel != null)
            {
                if (masteryLevel.IsTrained)
                    node.ImageIndex = imageList.Images.IndexOfKey(TrainedIcon);
                else if (masteryLevel.IsPartiallyTrained)
                    node.ImageIndex = imageList.Images.IndexOfKey(TrainableIcon);
                else
                    node.ImageIndex = imageList.Images.IndexOfKey(UntrainableIcon);
            }
            // The node represents a certificate level
            else if (certLevel != null)
            {
                if (certLevel.IsTrained)
                    node.ImageIndex = imageList.Images.IndexOfKey(TrainedIcon);
                else if (certLevel.IsPartiallyTrained)
                    node.ImageIndex = imageList.Images.IndexOfKey(TrainableIcon);
                else
                    node.ImageIndex = imageList.Images.IndexOfKey(UntrainableIcon);
            }
            // The node represents a skill prerequisite
            else
            {
                SkillLevel skillPrereq = (SkillLevel)node.Tag;
                Skill skill = m_character.Skills[skillPrereq.Skill.ID];

                if (skillPrereq.IsTrained)
                    node.ImageIndex = imageList.Images.IndexOfKey(TrainedIcon);
                else if (m_plan.IsPlanned(skill, skillPrereq.Level))
                    node.ImageIndex = imageList.Images.IndexOfKey(PlannedIcon);
                else if (skill.IsKnown)
                    node.ImageIndex = imageList.Images.IndexOfKey(TrainableIcon);
                else
                    node.ImageIndex = imageList.Images.IndexOfKey(UntrainableIcon);
            }

            // When selected, the image remains the same
            node.SelectedImageIndex = node.ImageIndex;

            // Update the children
            foreach (TreeNode child in node.Nodes)
            {
                UpdateNode(child);
            }
        }

        /// <summary>
        /// Custom draw for the label.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            // Prevents a bug that causes every item to be redrawn at the top left corner
            if (e.Bounds.Left <= 10)
            {
                e.DrawDefault = true;
                return;
            }

            string line2 = "-";
            int supIcon = -1;
            ImageList il;

            Mastery masteryLevel = e.Node.Tag as Mastery;
            CertificateLevel certLevel = e.Node.Tag as CertificateLevel;

            // Is it a mastery level ?
            if (masteryLevel != null)
            {
                if (!Settings.UI.SafeForWork)
                    supIcon = masteryLevel.Level;

                il = imageListMasteryLevels;

                // When not trained, let's display the training time of all certificates of this level
                if (!masteryLevel.IsTrained)
                {
                    line2 = masteryLevel.GetTrainingTime.ToDescriptiveText(DescriptiveTextOptions.IncludeCommas);
                }
            }
            else if (certLevel != null)
            {
                if (!Settings.UI.SafeForWork)
                    supIcon = (int)certLevel.Level;

                il = imageListCertLevels;

                // When not trained, let's display the training time
                if (!certLevel.IsTrained)
                {
                    line2 = certLevel.GetTrainingTime.ToDescriptiveText(DescriptiveTextOptions.IncludeCommas);
                }
            }
            // Or a skill prerequisite ?
            else
            {
                if (!Settings.UI.SafeForWork)
                    supIcon = imageList.Images.IndexOfKey(SkillBookIcon);

                il = imageList;
                SkillLevel skillPrereq = (SkillLevel)e.Node.Tag;

                // When not known to the require level, let's display the training time
                if (!skillPrereq.IsTrained)
                {
                    TimeSpan time = skillPrereq.Skill.GetLeftTrainingTimeToLevel(skillPrereq.Level);
                    line2 = time.ToDescriptiveText(DescriptiveTextOptions.IncludeCommas);
                }
            }

            // Choose colors according to selection
            bool isSelected = ((e.State & TreeNodeStates.Selected) == TreeNodeStates.Selected);
            Color backColor = (isSelected ? SystemColors.Highlight : treeView.BackColor);
            Color foreColor = (isSelected ? SystemColors.HighlightText : treeView.ForeColor);
            Color lightForeColor = (isSelected ? SystemColors.HighlightText : SystemColors.GrayText);

            // Draws the background
            using (SolidBrush background = new SolidBrush(backColor))
            {
                int width = treeView.ClientSize.Width - e.Bounds.Left;
                e.Graphics.FillRectangle(background, new Rectangle(e.Bounds.Left, e.Bounds.Top, width, e.Bounds.Height));
            }

            // Performs the drawing
            using (SolidBrush foreground = new SolidBrush(foreColor))
            {
                float offset;
                int left = e.Bounds.Left + il.ImageSize.Width + 2;
                Size line1Size = TextRenderer.MeasureText(e.Node.Text, m_boldFont);

                if (!String.IsNullOrEmpty(line2))
                {
                    Size line2Size = TextRenderer.MeasureText(line2, Font);

                    offset = (float)(e.Bounds.Height - line1Size.Height - line2Size.Height) / 2;
                    e.Graphics.DrawString(e.Node.Text, m_boldFont, foreground, new PointF(left, e.Bounds.Top + offset));

                    using (SolidBrush lightForeground = new SolidBrush(lightForeColor))
                    {
                        e.Graphics.DrawString(line2, Font, lightForeground, new PointF(left, e.Bounds.Top + offset + line1Size.Height));
                    }
                }
                else
                {
                    offset = (float)(e.Bounds.Height - line1Size.Height) / 2;
                    e.Graphics.DrawString(e.Node.Text, m_boldFont, foreground, new PointF(left, e.Bounds.Top + offset));
                }
            }

            // Draws the icon for skill/cert on the far right
            if (supIcon == -1)
                return;

            int imgOfssetX = e.Bounds.Left;
            float imgOffsetY = Math.Max(0.0f, (e.Bounds.Height - il.ImageSize.Height) * 0.5f);
            e.Graphics.DrawImageUnscaled(il.Images[supIcon], (imgOfssetX), (int)(e.Bounds.Top + imgOffsetY));
        }

        #endregion


        #region Context menus

        /// <summary>
        /// Context menu opening, we update the menus' statuses.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cmListSkills_Opening(object sender, CancelEventArgs e)
        {
            TreeNode node = treeView.SelectedNode;

            // Update "show in skill browser" menu
            showInMenuSeparator.Visible = false;
            showInBrowserMenu.Visible = false;
            showInExplorerMenu.Visible = false;

            if (node == null)
            {
                // Update "add to" menu
                tsmAddToPlan.Enabled = false;
                tsmAddToPlan.Text = @"Plan...";
            }
            else
            {
                Mastery masteryLevel = node.Tag as Mastery;
                CertificateLevel certLevel = node.Tag as CertificateLevel;

                if (masteryLevel != null)
                {
                    // Update "add to" menu
                    tsmAddToPlan.Enabled = !m_plan.WillGrantEligibilityFor(masteryLevel);
                    tsmAddToPlan.Text = String.Format(CultureConstants.DefaultCulture, "Plan \"{0}\"", masteryLevel);
                }
                // When a certificate is selected
                else if (certLevel != null)
                {
                    // Update "add to" menu
                    tsmAddToPlan.Enabled = !m_plan.WillGrantEligibilityFor(certLevel);
                    tsmAddToPlan.Text = String.Format(CultureConstants.DefaultCulture, "Plan \"{0}\"", certLevel.Certificate.Name);

                    showInMenuSeparator.Visible = true;

                    // Update "show in skill browser" menu
                    showInBrowserMenu.Visible = true;
                    showInBrowserMenu.Text = @"Show in Certificate Browser";
                }
                // When a skill is selected
                else
                {
                    // Update "add to" menu
                    SkillLevel prereq = (SkillLevel)node.Tag;
                    Skill skill = prereq.Skill;
                    tsmAddToPlan.Enabled = skill.Level < prereq.Level && !m_plan.IsPlanned(skill, prereq.Level);
                    tsmAddToPlan.Text = String.Format(CultureConstants.DefaultCulture, "Plan \"{0} {1}\"", skill,
                        Skill.GetRomanFromInt(prereq.Level));

                    // Update "show in skill browser" menu
                    showInMenuSeparator.Visible = true;
                    showInBrowserMenu.Visible = true;
                    showInBrowserMenu.Text = @"Show in Skill Browser";

                    // Update "show in skill explorer" menu
                    showInExplorerMenu.Visible = true;
                }
            }

            tsSeparatorToggle.Visible = (node != null && node.GetNodeCount(true) > 0);

            // "Collapse" and "Expand" menus
            tsmCollapseSelected.Visible = (node != null && node.GetNodeCount(true) > 0 && node.IsExpanded);
            tsmExpandSelected.Visible = (node != null && node.GetNodeCount(true) > 0 && !node.IsExpanded);

            tsmExpandSelected.Text = (node != null && node.GetNodeCount(true) > 0 && !node.IsExpanded
                ? String.Format(CultureConstants.DefaultCulture, "Expand {0}", node.Text)
                : String.Empty);
            tsmCollapseSelected.Text = (node != null && node.GetNodeCount(true) > 0 && node.IsExpanded
                ? String.Format(CultureConstants.DefaultCulture, "Collapse {0}", node.Text)
                : String.Empty);

            // "Expand All" and "Collapse All" menus
            tsmCollapseAll.Enabled = tsmCollapseAll.Visible = m_allExpanded;
            tsmExpandAll.Enabled = tsmExpandAll.Visible = !tsmCollapseAll.Enabled;
        }

        /// <summary>
        /// Treeview's context menu > Plan "(selection)".
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tsmAddToPlan_Click(object sender, EventArgs e)
        {
            Mastery masteryLevel = treeView.SelectedNode.Tag as Mastery;
            CertificateLevel certLevel = treeView.SelectedNode.Tag as CertificateLevel;
            IPlanOperation operation;

            if (masteryLevel != null)
                operation = m_plan.TryPlanTo(masteryLevel);
            else if (certLevel != null)
                operation = m_plan.TryPlanTo(certLevel);
            else
            {
                SkillLevel prereq = (SkillLevel)treeView.SelectedNode.Tag;
                operation = m_plan.TryPlanTo(prereq.Skill, prereq.Level);
            }

            if (operation == null)
                return;

            PlanWindow window = WindowsFactory.ShowByTag<PlanWindow, Plan>(operation.Plan);
            if (window == null || window.IsDisposed)
                return;

            PlanHelper.SelectPerform(new PlanToOperationForm(operation), window, operation);
        }

        /// <summary>
        /// Treeview's context menu > Expand.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tsmExpandSelected_Click(object sender, EventArgs e)
        {
            treeView.SelectedNode.Expand();
        }

        /// <summary>
        /// Treeview's context menu > Collapse.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tsmCollapseSelected_Click(object sender, EventArgs e)
        {
            treeView.SelectedNode.Collapse();
        }

        /// <summary>
        /// Treeview's context menu > Expand All.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tsmExpandAll_Click(object sender, EventArgs e)
        {
            treeView.ExpandAll();
            m_allExpanded = true;
        }

        /// <summary>
        /// Treeview's context menu > Collapse All.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tsmCollapseAll_Click(object sender, EventArgs e)
        {
            treeView.CollapseAll();
            m_allExpanded = false;
        }

        /// <summary>
        /// Context menu > Show "skill" in browser | Show "certificate class" certificates.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showInBrowserMenu_Click(object sender, EventArgs e)
        {
            // Retrieve the owner window
            PlanWindow planWindow = WindowsFactory.GetByTag<PlanWindow, Plan>(m_plan);
            if (planWindow == null || planWindow.IsDisposed)
                return;

            // Return when nothing is selected
            if (treeView.SelectedNode == null)
                return;

            var certLevel = treeView.SelectedNode.Tag as CertificateLevel;
            // When a certificate is selected, we select its class in the left tree
            if (certLevel != null)
                planWindow.ShowCertificateInBrowser(certLevel);
            // When a skill is selected, we select it in the skill browser
            else
            {
                SkillLevel skill = treeView.SelectedNode.Tag as SkillLevel;
                if (skill == null)
                    return;

                planWindow.ShowSkillInBrowser(skill.Skill);
            }
        }

        /// <summary>
        /// Context menu > Show "skill" in explorer.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showInExplorerMenu_Click(object sender, EventArgs e)
        {
            // Retrieve the owner window
            PlanWindow planWindow = WindowsFactory.GetByTag<PlanWindow, Plan>(m_plan);
            if (planWindow == null || planWindow.IsDisposed)
                return;

            // Return when nothing is selected
            if (treeView.SelectedNode == null)
                return;

            // Open the skill explorer
            SkillLevel prereq = (SkillLevel)treeView.SelectedNode.Tag;
            planWindow.ShowSkillInExplorer(prereq.Skill);
        }

        #endregion
    }
}