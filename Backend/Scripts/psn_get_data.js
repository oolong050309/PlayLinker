/**
 * PSN数据获取脚本
 * 使用已保存的令牌获取PSN用户数据(游戏列表、奖杯等)
 */

const {
  exchangeRefreshTokenForAuthTokens,
  getUserTitles,
  getUserTrophyProfileSummary,
  getProfileFromAccountId
} = require("psn-api");
const fs = require('fs');

// 解析命令行参数
function parseArguments() {
  const args = {};
  for (let i = 2; i < process.argv.length; i++) {
    if (process.argv[i].startsWith('--')) {
      const key = process.argv[i].substring(2);
      const value = process.argv[i + 1];
      args[key] = value;
      i++;
    }
  }
  return args;
}

// 从文件加载令牌
function loadTokens(tokensPath) {
  try {
    if (fs.existsSync(tokensPath)) {
      const data = fs.readFileSync(tokensPath, 'utf8');
      return JSON.parse(data);
    }
  } catch (error) {
    console.error(`ERROR: 加载令牌失败: ${error.message}`);
  }
  return null;
}

// 保存令牌到文件
function saveTokens(tokensPath, authData) {
  try {
    const tokenData = {
      ...authData,
      obtainedAt: Date.now()
    };
    fs.writeFileSync(tokensPath, JSON.stringify(tokenData, null, 2), 'utf8');
    console.log(`INFO: 令牌已更新并保存`);
    return true;
  } catch (error) {
    console.error(`ERROR: 保存令牌失败: ${error.message}`);
    return false;
  }
}

// 检查令牌是否过期
function isTokenExpired(tokenData) {
  if (!tokenData || !tokenData.obtainedAt) return true;
  const expiryTime = tokenData.obtainedAt + (tokenData.expiresIn * 1000);
  return Date.now() > expiryTime - 300000; // 提前5分钟认为过期
}

// 主函数
async function main() {
  try {
    const args = parseArguments();
    const tokensPath = args.tokens || './psn_tokens.json';

    // 加载令牌
    let authorization = loadTokens(tokensPath);
    
    if (!authorization) {
      console.log(JSON.stringify({
        success: false,
        message: "令牌文件不存在,请先进行认证",
        error: "token_not_found"
      }));
      process.exit(1);
    }

    // 检查令牌是否过期,如果过期则刷新
    if (isTokenExpired(authorization)) {
      console.log("INFO: 令牌已过期,尝试刷新...");
      
      if (!authorization.refreshToken) {
        console.log(JSON.stringify({
          success: false,
          message: "令牌已过期且没有刷新令牌,请重新认证",
          error: "token_expired"
        }));
        process.exit(1);
      }

      try {
        authorization = await exchangeRefreshTokenForAuthTokens(authorization.refreshToken);
        saveTokens(tokensPath, authorization);
        console.log("INFO: 令牌刷新成功");
      } catch (refreshError) {
        console.error(`ERROR: 刷新令牌失败: ${refreshError.message}`);
        console.log(JSON.stringify({
          success: false,
          message: `刷新令牌失败: ${refreshError.message}`,
          error: "token_refresh_failed"
        }));
        process.exit(1);
      }
    }

    // 解析账户信息
    let accountId = null;
    try {
      const accessToken = authorization.accessToken;
      const payload = JSON.parse(Buffer.from(accessToken.split('.')[1], 'base64').toString());
      accountId = payload.account_id;
      console.log(`INFO: 账户ID: ${accountId}`);
    } catch (parseError) {
      console.error(`ERROR: 解析令牌失败: ${parseError.message}`);
      console.log(JSON.stringify({
        success: false,
        message: `解析令牌失败: ${parseError.message}`,
        error: "token_parse_failed"
      }));
      process.exit(1);
    }

    // 获取数据
    console.log("INFO: 开始获取PSN数据");
    
    const result = {
      success: true,
      accountId: accountId,
      profile: null,
      trophySummary: null,
      userTitles: null
    };

    try {
      // 1. 获取用户资料
      console.log("INFO: 获取用户资料...");
      try {
        const profile = await getProfileFromAccountId(authorization, accountId);
        result.profile = profile;
        console.log(`INFO: 用户: ${profile.onlineId}`);
      } catch (profileError) {
        console.log(`WARNING: 获取用户资料失败: ${profileError.message}`);
      }

      // 2. 获取奖杯统计
      console.log("INFO: 获取奖杯统计...");
      try {
        const trophySummary = await getUserTrophyProfileSummary(authorization, accountId);
        result.trophySummary = trophySummary;
        console.log(`INFO: 奖杯等级: ${trophySummary.trophyLevel}`);
      } catch (trophyError) {
        console.log(`WARNING: 获取奖杯统计失败: ${trophyError.message}`);
      }

      // 3. 获取游戏列表(最多100个)
      console.log("INFO: 获取游戏列表...");
      try {
        const userTitlesResponse = await getUserTitles(authorization, accountId, { limit: 100 });
        result.userTitles = userTitlesResponse;
        console.log(`INFO: 找到 ${userTitlesResponse.trophyTitles.length} 个游戏`);
      } catch (titlesError) {
        console.log(`WARNING: 获取游戏列表失败: ${titlesError.message}`);
      }

      // 输出结果
      console.log("INFO: 数据获取完成");
      console.log(JSON.stringify(result));
      process.exit(0);

    } catch (dataError) {
      console.error(`ERROR: 获取数据失败: ${dataError.message}`);
      console.log(JSON.stringify({
        success: false,
        message: `获取数据失败: ${dataError.message}`,
        error: "data_fetch_failed",
        stack: dataError.stack
      }));
      process.exit(1);
    }

  } catch (error) {
    console.error(`ERROR: 未预期的错误: ${error.message}`);
    console.log(JSON.stringify({
      success: false,
      message: `未预期的错误: ${error.message}`,
      error: "unexpected_error",
      stack: error.stack
    }));
    process.exit(1);
  }
}

// 运行主函数
main();
