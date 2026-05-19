# 海康语音对讲 API

服务地址：`http://127.0.0.1:8888`（端口可在程序 API 输入框修改）

---

## GET /open

打开语音通道（自动登录 + 开始语音转发）。

**返回：**

```json
{"ok":true,"action":"open","msg":"channel opened"}
{"ok":false,"action":"open","msg":"login failed"}
```

---

## GET /close

关闭语音通道。

**返回：**

```json
{"ok":true,"action":"close","msg":"channel closed"}
```

---

## GET /status

查询当前状态。

**返回：**

```json
{"ok":true,"state":"open","loggedIn":true}
```

| 字段 | 说明 |
|------|------|
| state | `"open"` / `"closed"` |
| loggedIn | 是否已登录设备 |

---

## JavaScript 示例

```javascript
// 打开
const r = await fetch('http://127.0.0.1:8888/open');
console.log(await r.json());

// 关闭
await fetch('http://127.0.0.1:8888/close');

// 状态
const s = await fetch('http://127.0.0.1:8888/status');
const { state } = await s.json();
```

---

## 调用流程

```
GET /status  确认服务可用
GET /open    开始通话
GET /close   结束通话
```

## 备注

- 仅监听 `127.0.0.1`，不可远程访问
- 无需鉴权
- 跨域已开启（`Access-Control-Allow-Origin: *`）
