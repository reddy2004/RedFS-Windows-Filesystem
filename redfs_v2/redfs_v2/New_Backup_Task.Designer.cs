namespace redfs_v2
{
    partial class New_Backup_Task
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.dgv_backup_pairs = new System.Windows.Forms.DataGridView();
            this.sourcepath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Destpath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.button3 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_backup_pairs)).BeginInit();
            this.SuspendLayout();
            // 
            // dgv_backup_pairs
            // 
            this.dgv_backup_pairs.AllowUserToAddRows = false;
            this.dgv_backup_pairs.AllowUserToDeleteRows = false;
            this.dgv_backup_pairs.AllowUserToOrderColumns = true;
            this.dgv_backup_pairs.AllowUserToResizeRows = false;
            this.dgv_backup_pairs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgv_backup_pairs.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.sourcepath,
            this.Destpath});
            this.dgv_backup_pairs.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgv_backup_pairs.Location = new System.Drawing.Point(13, 83);
            this.dgv_backup_pairs.MultiSelect = false;
            this.dgv_backup_pairs.Name = "dgv_backup_pairs";
            this.dgv_backup_pairs.ReadOnly = true;
            this.dgv_backup_pairs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgv_backup_pairs.Size = new System.Drawing.Size(725, 274);
            this.dgv_backup_pairs.TabIndex = 0;
            // 
            // sourcepath
            // 
            this.sourcepath.HeaderText = "Source File/Folder";
            this.sourcepath.Name = "sourcepath";
            this.sourcepath.ReadOnly = true;
            this.sourcepath.Width = 500;
            // 
            // Destpath
            // 
            this.Destpath.HeaderText = "Destination File/Folder";
            this.Destpath.Name = "Destpath";
            this.Destpath.ReadOnly = true;
            this.Destpath.Width = 300;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Palatino Linotype", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.SystemColors.Highlight;
            this.label3.Location = new System.Drawing.Point(13, 64);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(211, 18);
            this.label3.TabIndex = 8;
            this.label3.Text = "Backup Folder-Pair / File-Pair List";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(51, 26);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(115, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "BACKUP TASK NAME";
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(172, 19);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(338, 20);
            this.textBox3.TabIndex = 10;
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.SystemColors.Menu;
            this.button3.Location = new System.Drawing.Point(644, 363);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(94, 23);
            this.button3.TabIndex = 11;
            this.button3.Text = "Add New";
            this.button3.UseVisualStyleBackColor = false;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(528, 425);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(75, 23);
            this.button4.TabIndex = 12;
            this.button4.Text = "Cancel";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(609, 425);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(129, 23);
            this.button1.TabIndex = 13;
            this.button1.Text = "Add Backup Task";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.SystemColors.Menu;
            this.button2.Location = new System.Drawing.Point(509, 363);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(129, 23);
            this.button2.TabIndex = 14;
            this.button2.Text = "Remove Selected";
            this.button2.UseVisualStyleBackColor = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(516, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(115, 13);
            this.label1.TabIndex = 16;
            this.label1.Text = "(Must be unique name)";
            // 
            // New_Backup_Task
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(754, 461);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.textBox3);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.dgv_backup_pairs);
            this.Name = "New_Backup_Task";
            this.Text = "New Backup Task";
            this.Load += new System.EventHandler(this.New_Backup_Job_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgv_backup_pairs)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dgv_backup_pairs;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.DataGridViewTextBoxColumn sourcepath;
        private System.Windows.Forms.DataGridViewTextBoxColumn Destpath;
        private System.Windows.Forms.Label label1;
    }
}