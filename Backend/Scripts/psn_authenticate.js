/**
 * PSN认证脚本
 * 使用NPSSO进行PSN认证,并保存令牌到文件
 */

const {
  exchangeNpssoForCode,
  exchangeCodeForAccessToken,
  exchangeRefreshTokenForAuthTokens,
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

// 保存令牌到文件
function saveTokens(tokensPath, authData) {
  try {
    const tokenData = {
      ...authData,
      obtainedAt: Date.now()
    };
    fs.writeFileSync(tokensPath, JSON.stringify(tokenData, null, 2), 'utf8');
    console.log(`INFO: 令牌已保存到 ${tokensPath}`);
    return true;
  } catch (error) {
    console.error(`ERROR: 保存令牌失败: ${error.message}`);
    return false;
  }
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
    const npsso = args.npsso;
    const tokensPath = args.tokens || './psn_tokens.json';

    if (!npsso) {
      console.log(JSON.stringify({
        success: false,
        message: "NPSSO参数为空。请提供 --npsso 参数",
        error: "missing_npsso"
      }));
      process.exit(1);
    }

    console.log("INFO: 开始PSN认证流程");

    // 先尝试加载已有令牌并刷新
    let authorization = null;
    const existingTokens = loadTokens(tokensPath);
    
    if (existingTokens && !isTokenExpired(existingTokens)) {
      console.log("INFO: 发现有效的已保存令牌,直接使用");
      authorization = existingTokens;
    } else if (existingTokens && existingTokens.refreshToken) {
      console.log("INFO: 令牌过期,尝试刷新...");
      try {
        authorization = await exchangeRefreshTokenForAuthTokens(existingTokens.refreshToken);
        saveTokens(tokensPath, authorization);
        console.log("INFO: 令牌刷新成功");
      } catch (refreshError) {
        console.log("WARNING: 刷新失败,进行全新认证...");
        authorization = null;
      }
    }

    // 如果没有有效令牌,使用NPSSO进行全新认证
    if (!authorization) {
      console.log("INFO: 使用NPSSO进行全新认证");
      
      try {
        // 第一步: 使用NPSSO换取授权码
        console.log("INFO: 步骤1 - 使用NPSSO换取授权码");
        const accessCode = await exchangeNpssoForCode(npsso);
        
        // 第二步: 使用授权码换取访问令牌
        console.log("INFO: 步骤2 - 使用授权码换取访问令牌");
        authorization = await exchangeCodeForAccessToken(accessCode);
        
        // 保存令牌
        if (saveTokens(tokensPath, authorization)) {
          console.log("INFO: 全新认证成功");
        } else {
          throw new Error("保存令牌失败");
        }
      } catch (authError) {
        console.error(`ERROR: 认证失败: ${authError.message}`);
        console.log(JSON.stringify({
          success: false,
          message: `认证失败: ${authError.message}`,
          error: "authentication_failed"
        }));
        process.exit(1);
      }
    }

    // 解析账户信息
    try {
      const accessToken = authorization.accessToken;
      const payload = JSON.parse(Buffer.from(accessToken.split('.')[1], 'base64').toString());
      const accountId = payload.account_id;
      
      console.log(`INFO: 账户ID: ${accountId}`);

      // 尝试获取用户资料
      let onlineId = null;
      try {
        const profile = await getProfileFromAccountId(authorization, accountId);
        onlineId = profile.onlineId;
        console.log(`INFO: 在线ID: ${onlineId}`);
      } catch (profileError) {
        console.log(`WARNING: 无法获取用户资料: ${profileError.message}`);
      }

      // 输出成功结果
      console.log(JSON.stringify({
        success: true,
        message: "PSN认证成功",
        accountId: accountId,
        onlineId: onlineId,
        tokensPath: tokensPath
      }));
      
      process.exit(0);
    } catch (parseError) {
      console.error(`ERROR: 解析账户信息失败: ${parseError.message}`);
      console.log(JSON.stringify({
        success: false,
        message: `解析账户信息失败: ${parseError.message}`,
        error: "parse_account_failed"
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
