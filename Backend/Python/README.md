# Xbox API Python脚本

这些Python脚本用于集成Xbox Live Web API，实现Xbox数据的获取和认证。

## 文件说明

- `xbox_authenticate.py` - Xbox Live认证脚本
- `xbox_get_data.py` - Xbox数据获取脚本
- `requirements.txt` - Python依赖列表
- `README.md` - 本文件

## 安装依赖

在部署服务器上执行以下命令安装Python依赖：

```bash
cd Backend/Python
pip install -r requirements.txt
```

或者使用国内镜像加速：

```bash
pip install -r requirements.txt -i https://pypi.tuna.tsinghua.edu.cn/simple
```

## 令牌存储

Xbox认证令牌会保存在 `Backend/Tokens/xbox_tokens.json`，请确保：
- 该目录有写入权限
- 令牌文件安全性（不要提交到版本控制）
- 定期备份令牌文件

## Python版本要求

- Python 3.8 或更高版本
- 推荐使用 Python 3.9 或 3.10

## 服务器配置

在 `appsettings.json` 中配置Python路径：

```json
{
  "XboxAPI": {
    "PythonPath": "python",
    "Note": "Windows服务器可能需要指定完整路径，如: C:\\Python39\\python.exe"
  }
}
```

### Linux服务器配置示例

```json
{
  "XboxAPI": {
    "PythonPath": "/usr/bin/python3"
  }
}
```

### Windows服务器配置示例

```json
{
  "XboxAPI": {
    "PythonPath": "C:\\Python39\\python.exe"
  }
}
```

## API使用流程

1. **认证**：调用 `POST /api/v1/xbox/authenticate` 进行Xbox Live认证
2. **导入数据**：调用 `POST /api/v1/xbox/import` 导入用户数据
3. **查询数据**：使用各种GET接口查询用户和游戏信息

## 注意事项

1. **首次使用需要人工认证**：首次认证时会打开浏览器进行OAuth2登录
2. **令牌有效期**：令牌会定期刷新，但长期不使用可能需要重新认证
3. **网络要求**：需要能够访问Xbox Live服务器（可能需要配置代理）
4. **账户要求**：必须使用成年账户（18岁以上）

## 故障排查

### 问题：Python脚本执行失败

- 检查Python是否正确安装：`python --version`
- 检查依赖是否安装：`pip list | grep xbox`
- 查看后端日志了解详细错误信息

### 问题：认证失败

- 确保使用的是成年账户
- 检查网络连接是否正常
- 尝试删除旧令牌文件重新认证

### 问题：导入数据为空

- 确保已完成认证
- 检查Xbox资料是否设置为公开
- 查看Python脚本输出日志

## 开发与测试

在开发环境中可以直接运行Python脚本测试：

```bash
# 认证
python xbox_authenticate.py --tokens "../Tokens/xbox_tokens.json"

# 获取数据
python xbox_get_data.py --tokens "../Tokens/xbox_tokens.json"
```

