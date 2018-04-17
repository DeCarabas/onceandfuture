/* eslint-env node */
module.exports = {
  mode: 'development',
  entry: './wwwroot/main.js',
  output: {
    path: __dirname  + '/wwwroot',
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
