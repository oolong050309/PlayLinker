# PSN API Scripts

这个目录包含了用于集成PSN API的Node.js脚本。

## 安装依赖

首次使用前,需要安装依赖:

```bash
npm install
```

这将安装 `psn-api` 包。

## 脚本说明

### 1. psn_authenticate.js
用于PSN认证,获取并保存访问令牌。

**用法**:
```bash
node psn_authenticate.js --npsso "你的NPSSO" --tokens "../Tokens/psn_tokens.json"
```

### 2. psn_get_data.js
使用已保存的令牌获取PSN用户数据(游戏列表、奖杯等)。

**用法**:
```bash
node psn_get_data.js --tokens "../Tokens/psn_tokens.json"
```

## 获取 NPSSO

1. 在浏览器中登录 PlayStation 账户
2. 访问: https://ca.account.sony.com/api/v1/ssocookie
3. 复制返回的 `npsso` 值

## 注意事项

- NPSSO 有效期约2个月
- 令牌会自动刷新
- 不要将 NPSSO 和令牌文件提交到代码仓库

## 依赖

- Node.js >= 14.x
- psn-api >= 2.9.0

## 更多信息

查看 `Backend/PSN_INTEGRATION.md` 获取完整文档。
