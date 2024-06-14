 /*
  * Copyright (c) 2023 RFMicron, Inc. dba Axzon Inc.
  *
  * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
  * THE SOFTWARE.
  */

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LedControls
{
    public class LedControl : CheckBox
    {
        public LedControl()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
            CheckedColor = Color.Red;
            UnCheckedColor = Color.Green;
            IndeterminateColor = Color.Gray;
        }
        [DefaultValue(typeof(Color), "Red")]
        public Color CheckedColor { get; set; }
        [DefaultValue(typeof(Color), "Green")]
        public Color UnCheckedColor { get; set; }
        [DefaultValue(typeof(Color), "Gray")]
        public Color IndeterminateColor { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var darkColor = Color.Black;
            var lightColor = Color.FromArgb(200, Color.White);
            var cornerAlpha = 80;
            this.OnPaintBackground(e);
            using (var path = new GraphicsPath())
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, Height, Height);
                path.AddEllipse(rect);
                rect.Inflate(-1, -1);
                using (var bgBrush = new SolidBrush(darkColor))
                {
                    e.Graphics.FillEllipse(bgBrush, rect);
                }
                using (var pathGrBrush = new PathGradientBrush(path))
                {
                    Color color = IndeterminateColor;
                    switch (CheckState)
                    {
                        case CheckState.Checked:
                            color = CheckedColor;
                            break;
                        case CheckState.Indeterminate:
                            color = IndeterminateColor;
                            break;
                        case CheckState.Unchecked:
                            color = UnCheckedColor;
                            break;
                    }
                    pathGrBrush.CenterColor = color; 
                    Color[] colors = { Color.FromArgb(cornerAlpha, color) };
                    pathGrBrush.SurroundColors = colors;
                    e.Graphics.FillEllipse(pathGrBrush, rect);
                }
                using (var pathGrBrush = new PathGradientBrush(path))
                {
                    pathGrBrush.CenterColor = lightColor;
                    Color[] colors = { Color.Transparent };
                    pathGrBrush.SurroundColors = colors;
                    var r = (float)(Math.Sqrt(2) * Height / 2);
                    var x = r / 8;
                    e.Graphics.FillEllipse(pathGrBrush, new RectangleF(-x, -x, r, r));
                    e.Graphics.ResetClip();
                }
            }
            TextRenderer.DrawText(e.Graphics, Text, Font,
                    new Rectangle(Height, 0, Width - Height, Height), ForeColor,
                     TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

    }
}
