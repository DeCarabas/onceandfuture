import { connect } from 'react-redux';
import { dismissBalloon } from '../actions';
import RiverBalloon from './riverballoon';

// The Balloon is a standard component; this component is one of those bound
// to the global state.

const mapStateToProps = (state) => {
  return {
    info: state.top_info,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    dispatchAction: dispatch,
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
