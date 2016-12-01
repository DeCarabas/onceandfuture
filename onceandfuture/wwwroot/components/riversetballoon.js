var React = require('react'); // N.B. Still need this because JSX.
import { connect } from 'react-redux'
import { dismissBalloon } from '../actions'
import {
  COLUMNWIDTH,
  RIVER_COLUMN_BACKGROUND_COLOR,
} from './style'
import RiverBalloon from './riverballoon'

const mapStateToProps = (state) => {
  return {
    info: state.top_info,
  }
};
const mapDispatchToProps = (dispatch) => {
  return {
    dispatch: dispatch,
    dismiss: () => dispatch(dismissBalloon()),
  };
};
const RiverSetBalloon = connect(
  mapStateToProps,
  mapDispatchToProps
)(
  RiverBalloon
);

export default RiverSetBalloon;