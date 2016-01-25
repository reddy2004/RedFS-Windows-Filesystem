/*
The license text is further down this page, and you should only download and use the source code 
if you agree to the terms in that text. For convenience, though, I’ve put together a human-readable 
(as opposed to lawyer-readable) non-authoritative interpretation of the license which will hopefully 
answer any questions you have. Basically, the license says that:

1. You can use the code in your own products.
2. You can modify the code as you wish, and use the modified code in your free products.
3. You can redistribute the original, unmodified code, but you have to include the full license text below.
4. You can redistribute the modified code as you wish (without the full license text below).
5. In all cases, you must include a credit mentioning 'Vikrama Reddy' as the original author of the source.
6. I'm not liable for anything you do with the code, no matter what. So be sensible.
7. You can't use my name or other marks to promote your products based on the code.
8. If you agree to all of that, go ahead and download the source. Otherwise, don't.
9. Derived work must have 'redfs' in the title. Ex. RedFS-advanced, Lite-Redfs, XRedfs etc.
*/

namespace redfs_v2
{
    partial class BackupJobDetailsUI
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
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.dgv_fldlist = new System.Windows.Forms.DataGridView();
            this.sourcepath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Destpath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.isfile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgv_joblist = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.JobID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.jobnum = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.backup_time = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Data_copied = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_fldlist)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_joblist)).BeginInit();
            this.SuspendLayout();
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(172, 12);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(338, 20);
            this.textBox3.TabIndex = 14;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(51, 15);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(115, 13);
            this.label4.TabIndex = 13;
            this.label4.Text = "BACKUP TASK NAME";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Palatino Linotype", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.SystemColors.Highlight;
            this.label3.Location = new System.Drawing.Point(13, 53);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(205, 18);
            this.label3.TabIndex = 12;
            this.label3.Text = "Backup Folder-Pair/File-Pair List";
            // 
            // dgv_fldlist
            // 
            this.dgv_fldlist.AllowUserToAddRows = false;
            this.dgv_fldlist.AllowUserToDeleteRows = false;
            this.dgv_fldlist.AllowUserToResizeRows = false;
            this.dgv_fldlist.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgv_fldlist.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.sourcepath,
            this.Destpath,
            this.isfile});
            this.dgv_fldlist.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgv_fldlist.Location = new System.Drawing.Point(16, 74);
            this.dgv_fldlist.MultiSelect = false;
            this.dgv_fldlist.Name = "dgv_fldlist";
            this.dgv_fldlist.ReadOnly = true;
            this.dgv_fldlist.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgv_fldlist.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgv_fldlist.Size = new System.Drawing.Size(725, 206);
            this.dgv_fldlist.TabIndex = 11;
            // 
            // sourcepath
            // 
            this.sourcepath.HeaderText = "Source Folder";
            this.sourcepath.Name = "sourcepath";
            this.sourcepath.ReadOnly = true;
            this.sourcepath.Width = 400;
            // 
            // Destpath
            // 
            this.Destpath.HeaderText = "Destination Folder";
            this.Destpath.Name = "Destpath";
            this.Destpath.ReadOnly = true;
            this.Destpath.Width = 200;
            // 
            // isfile
            // 
            this.isfile.HeaderText = "is file";
            this.isfile.Name = "isfile";
            this.isfile.ReadOnly = true;
            // 
            // dgv_joblist
            // 
            this.dgv_joblist.AllowUserToAddRows = false;
            this.dgv_joblist.AllowUserToDeleteRows = false;
            this.dgv_joblist.AllowUserToOrderColumns = true;
            this.dgv_joblist.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgv_joblist.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.JobID,
            this.jobnum,
            this.backup_time,
            this.Data_copied});
            this.dgv_joblist.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgv_joblist.Location = new System.Drawing.Point(16, 331);
            this.dgv_joblist.MultiSelect = false;
            this.dgv_joblist.Name = "dgv_joblist";
            this.dgv_joblist.ReadOnly = true;
            this.dgv_joblist.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgv_joblist.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgv_joblist.Size = new System.Drawing.Size(722, 262);
            this.dgv_joblist.TabIndex = 15;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Palatino Linotype", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.Highlight;
            this.label1.Location = new System.Drawing.Point(13, 310);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(169, 18);
            this.label1.TabIndex = 16;
            this.label1.Text = "Backup Runs (Job History)";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(663, 599);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 17;
            this.button1.Text = "Ok..";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // JobID
            // 
            this.JobID.HeaderText = "JobID";
            this.JobID.Name = "JobID";
            this.JobID.ReadOnly = true;
            // 
            // jobnum
            // 
            this.jobnum.HeaderText = "Job Name";
            this.jobnum.Name = "jobnum";
            this.jobnum.ReadOnly = true;
            // 
            // backup_time
            // 
            this.backup_time.HeaderText = "Backup Start Time";
            this.backup_time.Name = "backup_time";
            this.backup_time.ReadOnly = true;
            this.backup_time.Width = 150;
            // 
            // Data_copied
            // 
            this.Data_copied.HeaderText = "New Data Copied";
            this.Data_copied.Name = "Data_copied";
            this.Data_copied.ReadOnly = true;
            this.Data_copied.Width = 200;
            // 
            // BackupJobDetailsUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(754, 632);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dgv_joblist);
            this.Controls.Add(this.textBox3);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.dgv_fldlist);
            this.MaximumSize = new System.Drawing.Size(770, 670);
            this.MinimumSize = new System.Drawing.Size(770, 670);
            this.Name = "BackupJobDetailsUI";
            this.Text = "Backup Job Details";
            this.Load += new System.EventHandler(this.BackupJobDetailsUI_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgv_fldlist)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_joblist)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridView dgv_fldlist;
        private System.Windows.Forms.DataGridView dgv_joblist;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.DataGridViewTextBoxColumn sourcepath;
        private System.Windows.Forms.DataGridViewTextBoxColumn Destpath;
        private System.Windows.Forms.DataGridViewTextBoxColumn isfile;
        private System.Windows.Forms.DataGridViewTextBoxColumn JobID;
        private System.Windows.Forms.DataGridViewTextBoxColumn jobnum;
        private System.Windows.Forms.DataGridViewTextBoxColumn backup_time;
        private System.Windows.Forms.DataGridViewTextBoxColumn Data_copied;
    }
}