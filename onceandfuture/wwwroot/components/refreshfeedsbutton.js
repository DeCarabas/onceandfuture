import React from 'react';
import { connect } from 'react-redux';
import { refreshAllFeeds } from '../actions';
import IconButton from './iconbutton';


const RefreshFeedsButtonBase = ({loading, onRefresh, user}) => {
  if (loading) {
    return <div />;
  } else {
    return <IconButton
        onClick={() => onRefresh(user)}
        icon="/refresh.opt.svg"
        tip="Refresh all feeds."
        tipPosition="bottomleft"
      />;
  }
}

const mapStateToProps = (state) => {
  return {
    user: state.user,
    loading: state.loading,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onRefresh: (user) => dispatch(refreshAllFeeds(user)),
  };
};
const RefreshFeedsButton = connect(mapStateToProps, mapDispatchToProps)(RefreshFeedsButtonBase);

export default RefreshFeedsButton;
