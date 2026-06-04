const loginError = document.getElementById("loginError");
const params = new URLSearchParams(window.location.search);

if (params.get("loginError") === "1") {
  loginError.textContent = "\u5bc6\u7801\u4e0d\u6b63\u786e\uff0c\u8bf7\u91cd\u65b0\u8f93\u5165\u3002";
  loginError.hidden = false;
} else if (params.get("loginError") === "auth") {
  loginError.textContent = "\u8bf7\u5148\u767b\u5f55\u540e\u518d\u8bbf\u95ee\u89e6\u63a7\u677f\u3002";
  loginError.hidden = false;
}
