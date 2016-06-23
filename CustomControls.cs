using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.IO;
using System.Drawing.Text;
using Mono.Cecil;
using System.Threading.Tasks;
using System.ComponentModel;

namespace ETGModInstaller {
    public class CustomProgress : Control {
        
        public int Value = 0;
        public int Maximum = 100;
        
        public Brush BrushProgress;
        public Brush BrushText;
        
        protected override CreateParams CreateParams {
            get {
                CreateParams parms = base.CreateParams;
                parms.ExStyle |= 0x20; //WS_EX_TRANSPARENT
                return parms;
            }
        }
        
        public CustomProgress()
            : base() {
            SetStyle(ControlStyles.Opaque, false);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            
            BrushProgress = new SolidBrush(Color.FromArgb(127, 63, 255, 91));
            BrushText = new SolidBrush(Color.Black);
        }
        
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            
            g.FillRectangle(BrushProgress, 0, 0, (int) (e.ClipRectangle.Width * ((double) Value / (double) Maximum)), e.ClipRectangle.Height);
            
            SizeF textSize = g.MeasureString(Text, Font);
            g.DrawString(Text, Font, BrushText,
                (e.ClipRectangle.Width / 2f) - (textSize.Width / 2f),
                (e.ClipRectangle.Height / 2f) - (textSize.Height / 2f)
            );
        }
        
        protected override void Dispose(bool disposing) {
            BrushProgress.Dispose();
            BrushText.Dispose();
            base.Dispose(disposing);
        }
    }
    
}