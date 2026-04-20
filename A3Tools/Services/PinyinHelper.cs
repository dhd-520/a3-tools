using System.Text;
using NPinyin;

namespace A3Tools.Services;

/// <summary>
/// 拼音首字母帮助类 - 使用 NPinyin 库
/// </summary>
public static class PinyinHelper
{
    /// <summary>
    /// 获取汉字的拼音首字母
    /// </summary>
    public static string GetPinyinInitial(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var gb2312 = Encoding.GetEncoding("GB2312");
            
            // 获取全拼（按空格分隔每个字的全拼）
            var quanpin = Pinyin.GetPinyin(text, gb2312);
            
            // 按空格分割，取每个拼音的首字母
            var sb = new StringBuilder();
            foreach (var word in quanpin.Split(' '))
            {
                if (!string.IsNullOrEmpty(word))
                {
                    sb.Append(char.ToLower(word[0]));
                }
            }
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
