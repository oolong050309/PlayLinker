namespace PlayLinker.Services;

public static class EmailTemplates
{
    public static string Welcome(string username)
    {
        return $@"<div style='font-family:Segoe UI,Arial,sans-serif;'>
<h2>欢迎加入 PlayLinker</h2>
<p>Hi {System.Net.WebUtility.HtmlEncode(username)},</p>
<p>很高兴见到你！现在可以开始：</p>
<ul>
<li>绑定 Steam/Epic/GOG 等平台</li>
<li>管理游戏库与成就</li>
<li>配置价格提醒与家长监管</li>
</ul>
<p>祝你游戏愉快！</p>
<hr/>
<p style='color:#888'>本邮件由系统自动发送，请勿回复。</p>
</div>";
    }

    public static string PasswordReset(string username, string resetLink, int minutes)
    {
        return $@"<div style='font-family:Segoe UI,Arial,sans-serif;'>
<h2>PlayLinker 密码重置</h2>
<p>Hi {System.Net.WebUtility.HtmlEncode(username)},</p>
<p>请点击以下链接完成密码重置（{minutes}分钟内有效）：</p>
<p><a href='{System.Net.WebUtility.HtmlEncode(resetLink)}'>{System.Net.WebUtility.HtmlEncode(resetLink)}</a></p>
<p>如果不是你本人操作，请忽略本邮件。</p>
<hr/>
<p style='color:#888'>本邮件由系统自动发送，请勿回复。</p>
</div>";
    }
}

