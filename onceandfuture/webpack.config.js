/* eslint-env node */
module.exports = {
  entry: './wwwroot/main.js',
  output: {
    path: 'wwwroot',
    filename: 'bundle.js',
    pathinfo: true,
  },
  devtool: 'source-map',
  module: {
    rules: [
      {
        test: /\.jsx?$/,
        exclude: /(node_modules|bower_components)/,
        loader: 'babel-loader',
        options: {
          cacheDirectory: true,
        },
      }
    ]
  }
};
