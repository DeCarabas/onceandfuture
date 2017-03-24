/* eslint-env node */
const webpack = require('webpack');
module.exports = {
  entry: './wwwroot/main.js',
  output: {
    path: __dirname  + '/wwwroot',
    filename: 'bundle.js',
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
      },
    ],
  },
  plugins: [
    new webpack.DefinePlugin({
      'process.env': {
        'NODE_ENV': JSON.stringify('production')
      }
    }),
    new webpack.optimize.UglifyJsPlugin({
      compress: {
        dead_code: true,
        drop_debugger: true,
        warnings: true,
      },
      sourceMap: true,
    })
  ]
};
