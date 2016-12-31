/* eslint-env node */
module.exports = {
  entry: './wwwroot/main.js',
  output: {
    path: 'wwwroot',
    filename: 'bundle.js',
    pathinfo: true,
  },
  debug: true,
  devtool: 'source-map',
  module: {
    loaders: [
      {
        test: /\.jsx?$/,
        loaders: ['babel?cacheDirectory']
      }
    ]
  }
};
