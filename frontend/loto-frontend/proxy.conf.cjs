module.exports = {
  "/api": {
    target:
      process.env["services__loto-api__https__0"] ||
      process.env["services__loto-api__http__0"] ||
      "http://localhost:5100",
    secure: process.env["NODE_ENV"] !== "development",
    changeOrigin: true
  },
};
