
### WinForms Form 位置/尺寸设置顺序（2026-07-04 教训）

- **Form.Width / Form.Height / Form.Right 在 Show() 之前都是默认值（100x100）**
- **必须先 Show() 拿到 Handle，Width 才是真实值**
- **Show 之后用 Win32 SetWindowPos 强制设精确位置+大小**
- **不能用 Size / SetBounds 在 Show 之前定位**
- **Owner Form 自动限制子窗体位置（X 超出屏幕 → 夹回）**
- **多屏要用 Screen.FromControl(this).WorkingArea，不用 PrimaryScreen**
