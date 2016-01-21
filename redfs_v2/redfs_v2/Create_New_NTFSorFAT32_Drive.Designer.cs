namespace redfs_v2
{
    partial class Create_New_NTFSorFAT32_Drive
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
            this.label1 = new System.Windows.Forms.Label();
            this.drive_id_tb = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.drive_size_tb = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.newlun_ok = new System.Windows.Forms.Button();
            this.newlun_cancel = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(30, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(46, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Drive ID";
            // 
            // drive_id_tb
            // 
            this.drive_id_tb.Location = new System.Drawing.Point(82, 22);
            this.drive_id_tb.Name = "drive_id_tb";
            this.drive_id_tb.Size = new System.Drawing.Size(100, 20);
            this.drive_id_tb.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(49, 54);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(27, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Size";
            // 
            // drive_size_tb
            // 
            this.drive_size_tb.Location = new System.Drawing.Point(82, 51);
            this.drive_size_tb.Name = "drive_size_tb";
            this.drive_size_tb.Size = new System.Drawing.Size(100, 20);
            this.drive_size_tb.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(193, 54);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(81, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "GB (64GB Max)";
            // 
            // newlun_ok
            // 
            this.newlun_ok.Location = new System.Drawing.Point(199, 100);
            this.newlun_ok.Name = "newlun_ok";
            this.newlun_ok.Size = new System.Drawing.Size(75, 23);
            this.newlun_ok.TabIndex = 7;
            this.newlun_ok.Text = "OK";
            this.newlun_ok.UseVisualStyleBackColor = true;
            this.newlun_ok.Click += new System.EventHandler(this.newlun_ok_Click);
            // 
            // newlun_cancel
            // 
            this.newlun_cancel.Location = new System.Drawing.Point(118, 99);
            this.newlun_cancel.Name = "newlun_cancel";
            this.newlun_cancel.Size = new System.Drawing.Size(75, 23);
            this.newlun_cancel.TabIndex = 8;
            this.newlun_cancel.Text = "Cancel";
            this.newlun_cancel.UseVisualStyleBackColor = true;
            this.newlun_cancel.Click += new System.EventHandler(this.newlun_cancel_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(188, 25);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(86, 13);
            this.label5.TabIndex = 9;
            this.label5.Text = "(Must be unique)";
            // 
            // Create_New_NTFSorFAT32_Drive
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(325, 131);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.newlun_cancel);
            this.Controls.Add(this.newlun_ok);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.drive_size_tb);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.drive_id_tb);
            this.Controls.Add(this.label1);
            this.Name = "Create_New_NTFSorFAT32_Drive";
            this.Text = "Create_New_NTFSorFAT32_Drive";
            this.Load += new System.EventHandler(this.Create_New_NTFSorFAT32_Drive_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox drive_id_tb;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox drive_size_tb;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button newlun_ok;
        private System.Windows.Forms.Button newlun_cancel;
        private System.Windows.Forms.Label label5;
    }
}